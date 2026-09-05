using System.Security.Claims;
using EmpoloyeeManagment.Data;
using EmpoloyeeManagment.Dtos.Visits;
using EmpoloyeeManagment.Models;
using EmpoloyeeManagment.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace EmpoloyeeManagment.Endpoints;

public static class VisitEndpoints
{
    public static IEndpointRouteBuilder MapVisitEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/visits").WithTags("Visits").RequireAuthorization();

        group.MapGet("/", GetVisitsAsync);
        group.MapGet("/{id:int}", GetVisitByIdAsync);
        group.MapPost("/", CreateVisitAsync).RequireAuthorization("TaskAssigner");

        return app;
    }

    private static async Task<Ok<List<VisitListItemDto>>> GetVisitsAsync(ClaimsPrincipal principal, AppDbContext db)
    {
        var query = db.Visits.AsNoTracking()
            .Join(db.Tasks.AsNoTracking(), v => v.Id, t => t.RelatedVisitId, (v, t) => new { Visit = v, Task = t });

        if (!IsPrivileged(principal))
        {
            var caller = await GetCallerEmployeeAsync(principal, db);
            if (caller is null) return TypedResults.Ok(new List<VisitListItemDto>());
            query = query.Where(x => x.Task.AssigneeId == caller.Id);
        }

        var visits = await query
            .OrderBy(x => x.Visit.DateTime)
            .Select(x => new VisitListItemDto(x.Visit.Id, x.Visit.DateTime, x.Visit.ClientId, x.Visit.LocationId, x.Task.AssigneeId, x.Task.Status, x.Task.Id))
            .ToListAsync();

        return TypedResults.Ok(visits);
    }

    private static async Task<Results<Ok<VisitDetailDto>, NotFound>> GetVisitByIdAsync(int id, ClaimsPrincipal principal, AppDbContext db)
    {
        var visit = await db.Visits.AsNoTracking().FirstOrDefaultAsync(v => v.Id == id);
        if (visit is null) return TypedResults.NotFound();

        var task = await db.Tasks.AsNoTracking().FirstOrDefaultAsync(t => t.RelatedVisitId == id);
        if (task is null) return TypedResults.NotFound();

        if (!IsPrivileged(principal))
        {
            var caller = await GetCallerEmployeeAsync(principal, db);
            if (caller is null || task.AssigneeId != caller.Id) return TypedResults.NotFound();
        }

        return TypedResults.Ok(ToDetailDto(visit, task));
    }

    private static async Task<Results<Created<VisitDetailDto>, NotFound, BadRequest<string>>> CreateVisitAsync(
        CreateVisitRequest request, AppDbContext db, INotificationService notificationService)
    {
        var client = await db.Clients.FirstOrDefaultAsync(c => c.Id == request.ClientId);
        if (client is null) return TypedResults.NotFound();

        var location = await db.Locations.FirstOrDefaultAsync(l => l.Id == request.LocationId);
        if (location is null) return TypedResults.NotFound();
        if (location.ClientId != request.ClientId)
        {
            return TypedResults.BadRequest("The selected location does not belong to the selected client.");
        }

        var assignee = await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == request.AssigneeId);
        if (assignee is null) return TypedResults.NotFound();

        if (await HasActiveAcceptedVisitAsync(request.AssigneeId, db))
        {
            return TypedResults.BadRequest("This employee already has another accepted visit that isn't done yet.");
        }

        // A Visit and the Task that drives it are created together, or not at all —
        // mirrors the Employee+ApplicationUser transaction in AuthEndpoints.SignupAsync.
        await using var transaction = await db.Database.BeginTransactionAsync();

        var visit = new Visit
        {
            DateTime = request.DateTime,
            ClientId = request.ClientId,
            LocationId = request.LocationId,
            CreatedAt = DateTime.UtcNow
        };
        db.Visits.Add(visit);
        await db.SaveChangesAsync();

        var task = new TaskItem
        {
            Name = string.IsNullOrWhiteSpace(request.Name) ? $"Visit on {request.DateTime:yyyy-MM-dd HH:mm}" : request.Name,
            DueDateTime = request.DateTime,
            AssigneeId = request.AssigneeId,
            RelatedVisitId = visit.Id,
            Notes = request.Notes,
            AttendanceRequired = true,
            Status = TaskItemStatus.New,
            CreatedAt = DateTime.UtcNow
        };
        db.Tasks.Add(task);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        if (assignee.UserId is not null)
        {
            await notificationService.NotifyVisitAssignedAsync(visit.Id, assignee.UserId);
        }

        return TypedResults.Created($"/api/visits/{visit.Id}", ToDetailDto(visit, task));
    }

    private static Task<bool> HasActiveAcceptedVisitAsync(int employeeId, AppDbContext db) =>
        db.Tasks.AnyAsync(t => t.AssigneeId == employeeId && t.RelatedVisitId != null && t.Status == TaskItemStatus.InProgress);

    private static bool IsPrivileged(ClaimsPrincipal principal) =>
        principal.IsInRole(nameof(Role.Admin)) || principal.IsInRole(nameof(Role.Manager)) || principal.IsInRole(nameof(Role.Supervisor));

    private static async Task<Employee?> GetCallerEmployeeAsync(ClaimsPrincipal principal, AppDbContext db)
    {
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return userId is null ? null : await db.Employees.FirstOrDefaultAsync(e => e.UserId == userId);
    }

    private static VisitDetailDto ToDetailDto(Visit visit, TaskItem task) => new(
        visit.Id, visit.DateTime, visit.ClientId, visit.LocationId, task.AssigneeId, task.Status, task.Id, visit.CreatedAt);
}
