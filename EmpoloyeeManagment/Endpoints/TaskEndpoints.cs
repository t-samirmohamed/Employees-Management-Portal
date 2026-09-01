using System.Security.Claims;
using EmpoloyeeManagment.Data;
using EmpoloyeeManagment.Dtos.Tasks;
using EmpoloyeeManagment.Models;
using EmpoloyeeManagment.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace EmpoloyeeManagment.Endpoints;

public static class TaskEndpoints
{
    public static IEndpointRouteBuilder MapTaskEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tasks").WithTags("Tasks").RequireAuthorization();

        group.MapGet("/", GetTasksAsync);
        group.MapGet("/{id:int}", GetTaskByIdAsync);
        group.MapPost("/", CreateTaskAsync).RequireAuthorization("TaskAssigner");
        group.MapPatch("/{id:int}", ReassignTaskAsync).RequireAuthorization("TaskAssigner");
        group.MapPatch("/{id:int}/accept", AcceptTaskAsync).RequireAuthorization("EmployeeOnly");
        group.MapPatch("/{id:int}/reject", RejectTaskAsync).RequireAuthorization("EmployeeOnly");
        group.MapPatch("/{id:int}/cancel", CancelTaskAsync).RequireAuthorization("TaskAssigner");
        group.MapPatch("/{id:int}/complete", CompleteTaskAsync).RequireAuthorization("TaskAssigner");
        group.MapPost("/{id:int}/comments", AddCommentAsync);
        group.MapPatch("/{id:int}/comments/{commentId:int}", UpdateCommentAsync);
        group.MapDelete("/{id:int}/comments/{commentId:int}", DeleteCommentAsync);

        return app;
    }

    private static async Task<Ok<List<TaskListItemDto>>> GetTasksAsync(ClaimsPrincipal principal, AppDbContext db)
    {
        var query = db.Tasks.AsNoTracking().AsQueryable();

        if (!IsPrivileged(principal))
        {
            var caller = await GetCallerEmployeeAsync(principal, db);
            if (caller is null) return TypedResults.Ok(new List<TaskListItemDto>());
            query = query.Where(t => t.AssigneeId == caller.Id);
        }

        var tasks = await query
            .OrderBy(t => t.DueDateTime)
            .Select(t => new TaskListItemDto(t.Id, t.Name, t.DueDateTime, t.AssigneeId, t.RelatedVisitId, t.Status, t.AttendanceRequired))
            .ToListAsync();

        return TypedResults.Ok(tasks);
    }

    private static async Task<Results<Ok<TaskDetailResponse>, NotFound>> GetTaskByIdAsync(int id, ClaimsPrincipal principal, AppDbContext db)
    {
        var task = await db.Tasks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
        if (task is null) return TypedResults.NotFound();
        if (!await CanViewTaskAsync(task, principal, db)) return TypedResults.NotFound();

        var comments = await db.TaskComments.AsNoTracking()
            .Where(c => c.TaskId == id)
            .OrderBy(c => c.CreatedAt)
            .Select(c => new TaskCommentDto(c.Id, c.AuthorEmployeeId, c.Text, c.CreatedAt, c.UpdatedAt))
            .ToListAsync();

        return TypedResults.Ok(new TaskDetailResponse(ToDetailDto(task), comments));
    }

    private static async Task<Results<Created<TaskDetailDto>, NotFound, BadRequest<string>>> CreateTaskAsync(CreateTaskRequest request, AppDbContext db)
    {
        var assigneeExists = await db.Employees.AnyAsync(e => e.Id == request.AssigneeId);
        if (!assigneeExists) return TypedResults.NotFound();

        if (request.RelatedVisitId is not null)
        {
            var visitExists = await db.Visits.AnyAsync(v => v.Id == request.RelatedVisitId);
            if (!visitExists) return TypedResults.NotFound();

            var alreadyLinked = await db.Tasks.AnyAsync(t => t.RelatedVisitId == request.RelatedVisitId);
            if (alreadyLinked) return TypedResults.BadRequest("This visit is already linked to another task.");
        }

        var task = new TaskItem
        {
            Name = request.Name,
            DueDateTime = request.DueDateTime,
            AssigneeId = request.AssigneeId,
            RelatedVisitId = request.RelatedVisitId,
            Notes = request.Notes,
            AttendanceRequired = request.AttendanceRequired,
            Status = TaskItemStatus.New,
            CreatedAt = DateTime.UtcNow
        };

        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        return TypedResults.Created($"/api/tasks/{task.Id}", ToDetailDto(task));
    }

    private static async Task<Results<Ok<TaskDetailDto>, NotFound, BadRequest<string>>> ReassignTaskAsync(
        int id, ReassignTaskRequest request, AppDbContext db, IEmployeeAttendanceService attendanceService)
    {
        var task = await db.Tasks.FirstOrDefaultAsync(t => t.Id == id);
        if (task is null) return TypedResults.NotFound();

        if (task.Status is TaskItemStatus.Done or TaskItemStatus.Cancelled)
        {
            return TypedResults.BadRequest($"Cannot reassign a task with status {task.Status}.");
        }

        var assigneeExists = await db.Employees.AnyAsync(e => e.Id == request.NewAssigneeId);
        if (!assigneeExists) return TypedResults.NotFound();

        var previousAssigneeId = task.AssigneeId;
        var wasInProgress = task.Status == TaskItemStatus.InProgress;

        task.AssigneeId = request.NewAssigneeId;
        task.Status = TaskItemStatus.New;
        task.RejectionReason = null;
        await db.SaveChangesAsync();

        if (wasInProgress && task.AttendanceRequired)
        {
            await attendanceService.RecalculateAsync(previousAssigneeId);
        }

        return TypedResults.Ok(ToDetailDto(task));
    }

    private static async Task<Results<Ok<TaskDetailDto>, NotFound, BadRequest<string>>> AcceptTaskAsync(
        int id, ClaimsPrincipal principal, AppDbContext db, IEmployeeAttendanceService attendanceService)
    {
        var task = await db.Tasks.FirstOrDefaultAsync(t => t.Id == id);
        if (task is null) return TypedResults.NotFound();

        var caller = await GetCallerEmployeeAsync(principal, db);
        if (caller is null || task.AssigneeId != caller.Id) return TypedResults.NotFound();

        if (task.Status != TaskItemStatus.New)
        {
            return TypedResults.BadRequest($"Cannot accept a task with status {task.Status}.");
        }

        if (task.RelatedVisitId is not null && await HasActiveAcceptedVisitAsync(task.AssigneeId, db))
        {
            return TypedResults.BadRequest("This employee already has another accepted visit that isn't done yet.");
        }

        task.Status = TaskItemStatus.InProgress;
        await db.SaveChangesAsync();

        if (task.AttendanceRequired)
        {
            await attendanceService.RecalculateAsync(task.AssigneeId);
        }

        return TypedResults.Ok(ToDetailDto(task));
    }

    private static async Task<Results<Ok<TaskDetailDto>, NotFound, BadRequest<string>>> RejectTaskAsync(
        int id, RejectTaskRequest request, ClaimsPrincipal principal, AppDbContext db)
    {
        var task = await db.Tasks.FirstOrDefaultAsync(t => t.Id == id);
        if (task is null) return TypedResults.NotFound();

        var caller = await GetCallerEmployeeAsync(principal, db);
        if (caller is null || task.AssigneeId != caller.Id) return TypedResults.NotFound();

        if (task.Status != TaskItemStatus.New)
        {
            return TypedResults.BadRequest($"Cannot reject a task with status {task.Status}.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return TypedResults.BadRequest("A reason is required to reject a task.");
        }

        task.Status = TaskItemStatus.Rejected;
        task.RejectionReason = request.Reason;
        await db.SaveChangesAsync();

        return TypedResults.Ok(ToDetailDto(task));
    }

    private static async Task<Results<Ok<TaskDetailDto>, NotFound, BadRequest<string>>> CancelTaskAsync(
        int id, AppDbContext db, IEmployeeAttendanceService attendanceService)
    {
        var task = await db.Tasks.FirstOrDefaultAsync(t => t.Id == id);
        if (task is null) return TypedResults.NotFound();

        if (task.Status is not (TaskItemStatus.New or TaskItemStatus.InProgress))
        {
            return TypedResults.BadRequest($"Cannot cancel a task with status {task.Status}.");
        }

        var wasInProgress = task.Status == TaskItemStatus.InProgress;
        task.Status = TaskItemStatus.Cancelled;
        await db.SaveChangesAsync();

        if (wasInProgress && task.AttendanceRequired)
        {
            await attendanceService.RecalculateAsync(task.AssigneeId);
        }

        return TypedResults.Ok(ToDetailDto(task));
    }

    private static async Task<Results<Ok<TaskDetailDto>, NotFound, BadRequest<string>>> CompleteTaskAsync(
        int id, AppDbContext db, IEmployeeAttendanceService attendanceService)
    {
        var task = await db.Tasks.FirstOrDefaultAsync(t => t.Id == id);
        if (task is null) return TypedResults.NotFound();

        if (task.Status != TaskItemStatus.InProgress)
        {
            return TypedResults.BadRequest($"Cannot complete a task with status {task.Status}.");
        }

        task.Status = TaskItemStatus.Done;
        await db.SaveChangesAsync();

        if (task.AttendanceRequired)
        {
            await attendanceService.RecalculateAsync(task.AssigneeId);
        }

        return TypedResults.Ok(ToDetailDto(task));
    }

    private static async Task<Results<Created<TaskCommentDto>, NotFound, BadRequest<string>>> AddCommentAsync(
        int id, AddCommentRequest request, ClaimsPrincipal principal, AppDbContext db)
    {
        var task = await db.Tasks.FirstOrDefaultAsync(t => t.Id == id);
        if (task is null) return TypedResults.NotFound();
        if (!await CanViewTaskAsync(task, principal, db)) return TypedResults.NotFound();

        if (string.IsNullOrWhiteSpace(request.Text)) return TypedResults.BadRequest("Comment text is required.");

        var caller = await GetCallerEmployeeAsync(principal, db);
        if (caller is null) return TypedResults.NotFound();

        var comment = new TaskComment
        {
            TaskId = id,
            AuthorEmployeeId = caller.Id,
            Text = request.Text,
            CreatedAt = DateTime.UtcNow
        };
        db.TaskComments.Add(comment);
        await db.SaveChangesAsync();

        return TypedResults.Created($"/api/tasks/{id}/comments/{comment.Id}", ToCommentDto(comment));
    }

    private static async Task<Results<Ok<TaskCommentDto>, NotFound, ForbidHttpResult>> UpdateCommentAsync(
        int id, int commentId, UpdateCommentRequest request, ClaimsPrincipal principal, AppDbContext db)
    {
        var comment = await db.TaskComments.FirstOrDefaultAsync(c => c.Id == commentId && c.TaskId == id);
        if (comment is null) return TypedResults.NotFound();

        var caller = await GetCallerEmployeeAsync(principal, db);
        var isAuthor = caller is not null && comment.AuthorEmployeeId == caller.Id;
        if (!isAuthor && !principal.IsInRole(nameof(Role.Admin))) return TypedResults.Forbid();

        comment.Text = request.Text;
        comment.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return TypedResults.Ok(ToCommentDto(comment));
    }

    private static async Task<Results<NoContent, NotFound, ForbidHttpResult>> DeleteCommentAsync(
        int id, int commentId, ClaimsPrincipal principal, AppDbContext db)
    {
        var comment = await db.TaskComments.FirstOrDefaultAsync(c => c.Id == commentId && c.TaskId == id);
        if (comment is null) return TypedResults.NotFound();

        var caller = await GetCallerEmployeeAsync(principal, db);
        var isAuthor = caller is not null && comment.AuthorEmployeeId == caller.Id;
        if (!isAuthor && !principal.IsInRole(nameof(Role.Admin))) return TypedResults.Forbid();

        db.TaskComments.Remove(comment);
        await db.SaveChangesAsync();

        return TypedResults.NoContent();
    }

    private static bool IsPrivileged(ClaimsPrincipal principal) =>
        principal.IsInRole(nameof(Role.Admin)) || principal.IsInRole(nameof(Role.Manager)) || principal.IsInRole(nameof(Role.Supervisor));

    private static async Task<Employee?> GetCallerEmployeeAsync(ClaimsPrincipal principal, AppDbContext db)
    {
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return userId is null ? null : await db.Employees.FirstOrDefaultAsync(e => e.UserId == userId);
    }

    private static async Task<bool> CanViewTaskAsync(TaskItem task, ClaimsPrincipal principal, AppDbContext db)
    {
        if (IsPrivileged(principal)) return true;
        var caller = await GetCallerEmployeeAsync(principal, db);
        return caller is not null && task.AssigneeId == caller.Id;
    }

    private static Task<bool> HasActiveAcceptedVisitAsync(int employeeId, AppDbContext db) =>
        db.Tasks.AnyAsync(t => t.AssigneeId == employeeId && t.RelatedVisitId != null && t.Status == TaskItemStatus.InProgress);

    private static TaskDetailDto ToDetailDto(TaskItem task) => new(
        task.Id, task.Name, task.DueDateTime, task.AssigneeId, task.RelatedVisitId,
        task.Notes, task.AttendanceRequired, task.Status, task.RejectionReason, task.CreatedAt);

    private static TaskCommentDto ToCommentDto(TaskComment comment) => new(
        comment.Id, comment.AuthorEmployeeId, comment.Text, comment.CreatedAt, comment.UpdatedAt);
}
