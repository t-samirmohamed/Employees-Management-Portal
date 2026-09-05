using System.Security.Claims;
using EmpoloyeeManagment.Data;
using EmpoloyeeManagment.Dtos.Leaves;
using EmpoloyeeManagment.Models;
using EmpoloyeeManagment.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace EmpoloyeeManagment.Endpoints;

public static class LeaveEndpoints
{
    public static IEndpointRouteBuilder MapLeaveEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/leaves").WithTags("Leaves").RequireAuthorization();

        group.MapGet("/", GetLeaveRequestsAsync);
        group.MapGet("/{id:int}", GetLeaveRequestByIdAsync);
        group.MapPost("/", CreateLeaveRequestAsync).RequireAuthorization("LeaveRequester");
        group.MapPatch("/{id:int}", UpdateLeaveRequestAsync);
        group.MapDelete("/{id:int}", DeleteLeaveRequestAsync);
        group.MapPatch("/{id:int}/accept", AcceptLeaveRequestAsync).RequireAuthorization("TaskAssigner");
        group.MapPatch("/{id:int}/reject", RejectLeaveRequestAsync).RequireAuthorization("TaskAssigner");
        group.MapPatch("/{id:int}/request-delay", RequestDelayAsync).RequireAuthorization("TaskAssigner");

        return app;
    }

    private static async Task<Ok<List<LeaveRequestListItemDto>>> GetLeaveRequestsAsync(ClaimsPrincipal principal, AppDbContext db)
    {
        var query = db.LeaveRequests.AsNoTracking().AsQueryable();

        if (!IsPrivileged(principal))
        {
            var caller = await GetCallerEmployeeAsync(principal, db);
            if (caller is null) return TypedResults.Ok(new List<LeaveRequestListItemDto>());
            query = query.Where(l => l.RequesterId == caller.Id);
        }

        var leaveRequests = await query
            .OrderByDescending(l => l.CreatedAt)
            .Select(l => new LeaveRequestListItemDto(l.Id, l.RequesterId, l.StartDate, l.EndDate, l.Status, l.ApproverEmployeeId))
            .ToListAsync();

        return TypedResults.Ok(leaveRequests);
    }

    private static async Task<Results<Ok<LeaveRequestDetailDto>, NotFound>> GetLeaveRequestByIdAsync(int id, ClaimsPrincipal principal, AppDbContext db)
    {
        var leaveRequest = await db.LeaveRequests.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id);
        if (leaveRequest is null) return TypedResults.NotFound();

        if (!IsPrivileged(principal))
        {
            var caller = await GetCallerEmployeeAsync(principal, db);
            if (caller is null || leaveRequest.RequesterId != caller.Id) return TypedResults.NotFound();
        }

        return TypedResults.Ok(ToDetailDto(leaveRequest));
    }

    private static async Task<Results<Created<LeaveRequestDetailDto>, NotFound, BadRequest<string>>> CreateLeaveRequestAsync(
        CreateLeaveRequestRequest request, ClaimsPrincipal principal, AppDbContext db, INotificationService notificationService)
    {
        var caller = await GetCallerEmployeeAsync(principal, db);
        if (caller is null) return TypedResults.NotFound();

        if (request.EndDate < request.StartDate)
        {
            return TypedResults.BadRequest("EndDate cannot be before StartDate.");
        }

        var leaveRequest = new LeaveRequest
        {
            RequesterId = caller.Id,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Reason = request.Reason,
            Status = LeaveRequestStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        db.LeaveRequests.Add(leaveRequest);
        await db.SaveChangesAsync();

        var approverUserIds = await GetEligibleApproverUserIdsAsync(caller.Id, db);
        if (approverUserIds.Count > 0)
        {
            await notificationService.NotifyLeaveRequestSubmittedAsync(leaveRequest.Id, approverUserIds);
        }

        return TypedResults.Created($"/api/leaves/{leaveRequest.Id}", ToDetailDto(leaveRequest));
    }

    private static async Task<Results<Ok<LeaveRequestDetailDto>, NotFound, BadRequest<string>, ForbidHttpResult>> UpdateLeaveRequestAsync(
        int id, UpdateLeaveRequestRequest request, ClaimsPrincipal principal, AppDbContext db)
    {
        var leaveRequest = await db.LeaveRequests.FirstOrDefaultAsync(l => l.Id == id);
        if (leaveRequest is null) return TypedResults.NotFound();

        var caller = await GetCallerEmployeeAsync(principal, db);
        if (caller is null || leaveRequest.RequesterId != caller.Id) return TypedResults.Forbid();

        if (leaveRequest.Status != LeaveRequestStatus.Pending)
        {
            return TypedResults.BadRequest($"Cannot edit a leave request with status {leaveRequest.Status}.");
        }

        if (request.EndDate < request.StartDate)
        {
            return TypedResults.BadRequest("EndDate cannot be before StartDate.");
        }

        leaveRequest.StartDate = request.StartDate;
        leaveRequest.EndDate = request.EndDate;
        leaveRequest.Reason = request.Reason;
        leaveRequest.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return TypedResults.Ok(ToDetailDto(leaveRequest));
    }

    private static async Task<Results<NoContent, NotFound, BadRequest<string>, ForbidHttpResult>> DeleteLeaveRequestAsync(
        int id, ClaimsPrincipal principal, AppDbContext db)
    {
        var leaveRequest = await db.LeaveRequests.FirstOrDefaultAsync(l => l.Id == id);
        if (leaveRequest is null) return TypedResults.NotFound();

        var caller = await GetCallerEmployeeAsync(principal, db);
        if (caller is null || leaveRequest.RequesterId != caller.Id) return TypedResults.Forbid();

        if (leaveRequest.Status != LeaveRequestStatus.Pending)
        {
            return TypedResults.BadRequest($"Cannot delete a leave request with status {leaveRequest.Status}.");
        }

        db.LeaveRequests.Remove(leaveRequest);
        await db.SaveChangesAsync();

        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<LeaveRequestDetailDto>, NotFound, BadRequest<string>, ForbidHttpResult>> AcceptLeaveRequestAsync(
        int id, ClaimsPrincipal principal, AppDbContext db, INotificationService notificationService)
    {
        var leaveRequest = await db.LeaveRequests.FirstOrDefaultAsync(l => l.Id == id);
        if (leaveRequest is null) return TypedResults.NotFound();

        var requesterRole = await GetEmployeeRoleAsync(leaveRequest.RequesterId, db);
        if (requesterRole is null) return TypedResults.NotFound();
        if (!CanActionLeaveRequest(principal, requesterRole.Value)) return TypedResults.Forbid();

        if (leaveRequest.Status != LeaveRequestStatus.Pending)
        {
            return TypedResults.BadRequest($"Cannot action a leave request with status {leaveRequest.Status}.");
        }

        var caller = await GetCallerEmployeeAsync(principal, db);
        if (caller is null) return TypedResults.NotFound();

        leaveRequest.Status = LeaveRequestStatus.Accepted;
        leaveRequest.ApproverEmployeeId = caller.Id;
        await db.SaveChangesAsync();

        var requesterUserId = await GetEmployeeUserIdAsync(leaveRequest.RequesterId, db);
        if (requesterUserId is not null)
        {
            await notificationService.NotifyLeaveRequestAcceptedAsync(leaveRequest.Id, requesterUserId);
        }

        return TypedResults.Ok(ToDetailDto(leaveRequest));
    }

    private static async Task<Results<Ok<LeaveRequestDetailDto>, NotFound, BadRequest<string>, ForbidHttpResult>> RejectLeaveRequestAsync(
        int id, RejectLeaveRequestRequest request, ClaimsPrincipal principal, AppDbContext db, INotificationService notificationService)
    {
        var leaveRequest = await db.LeaveRequests.FirstOrDefaultAsync(l => l.Id == id);
        if (leaveRequest is null) return TypedResults.NotFound();

        var requesterRole = await GetEmployeeRoleAsync(leaveRequest.RequesterId, db);
        if (requesterRole is null) return TypedResults.NotFound();
        if (!CanActionLeaveRequest(principal, requesterRole.Value)) return TypedResults.Forbid();

        if (leaveRequest.Status != LeaveRequestStatus.Pending)
        {
            return TypedResults.BadRequest($"Cannot action a leave request with status {leaveRequest.Status}.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return TypedResults.BadRequest("A reason is required to reject a leave request.");
        }

        var caller = await GetCallerEmployeeAsync(principal, db);
        if (caller is null) return TypedResults.NotFound();

        leaveRequest.Status = LeaveRequestStatus.Rejected;
        leaveRequest.ApproverEmployeeId = caller.Id;
        leaveRequest.DecisionReason = request.Reason;
        await db.SaveChangesAsync();

        var requesterUserId = await GetEmployeeUserIdAsync(leaveRequest.RequesterId, db);
        if (requesterUserId is not null)
        {
            await notificationService.NotifyLeaveRequestRejectedAsync(leaveRequest.Id, requesterUserId);
        }

        return TypedResults.Ok(ToDetailDto(leaveRequest));
    }

    private static async Task<Results<Ok<LeaveRequestDetailDto>, NotFound, BadRequest<string>, ForbidHttpResult>> RequestDelayAsync(
        int id, RequestDelayRequest request, ClaimsPrincipal principal, AppDbContext db, INotificationService notificationService)
    {
        var leaveRequest = await db.LeaveRequests.FirstOrDefaultAsync(l => l.Id == id);
        if (leaveRequest is null) return TypedResults.NotFound();

        var requesterRole = await GetEmployeeRoleAsync(leaveRequest.RequesterId, db);
        if (requesterRole is null) return TypedResults.NotFound();
        if (!CanActionLeaveRequest(principal, requesterRole.Value)) return TypedResults.Forbid();

        if (leaveRequest.Status != LeaveRequestStatus.Pending)
        {
            return TypedResults.BadRequest($"Cannot action a leave request with status {leaveRequest.Status}.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return TypedResults.BadRequest("A reason is required to request a delay.");
        }

        var caller = await GetCallerEmployeeAsync(principal, db);
        if (caller is null) return TypedResults.NotFound();

        leaveRequest.Status = LeaveRequestStatus.DelayRequested;
        leaveRequest.ApproverEmployeeId = caller.Id;
        leaveRequest.DecisionReason = request.Reason;
        leaveRequest.RequestedDelayDate = request.TargetDate;
        await db.SaveChangesAsync();

        var requesterUserId = await GetEmployeeUserIdAsync(leaveRequest.RequesterId, db);
        if (requesterUserId is not null)
        {
            await notificationService.NotifyLeaveRequestDelayRequestedAsync(leaveRequest.Id, requesterUserId);
        }

        return TypedResults.Ok(ToDetailDto(leaveRequest));
    }

    private static bool IsPrivileged(ClaimsPrincipal principal) =>
        principal.IsInRole(nameof(Role.Admin)) || principal.IsInRole(nameof(Role.Manager)) || principal.IsInRole(nameof(Role.Supervisor));

    private static async Task<Employee?> GetCallerEmployeeAsync(ClaimsPrincipal principal, AppDbContext db)
    {
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return userId is null ? null : await db.Employees.FirstOrDefaultAsync(e => e.UserId == userId);
    }

    private static async Task<Role?> GetEmployeeRoleAsync(int employeeId, AppDbContext db)
    {
        var roleName = await db.Employees
            .Where(e => e.Id == employeeId && e.UserId != null)
            .Select(e => db.Roles
                .Where(r => db.UserRoles.Any(ur => ur.UserId == e.UserId && ur.RoleId == r.Id))
                .Select(r => r.Name)
                .FirstOrDefault())
            .FirstOrDefaultAsync();

        return roleName is null ? null : Enum.Parse<Role>(roleName);
    }

    private static Task<string?> GetEmployeeUserIdAsync(int employeeId, AppDbContext db) =>
        db.Employees.Where(e => e.Id == employeeId).Select(e => e.UserId).FirstOrDefaultAsync();

    // Recipients for a "leave request submitted" notification: everyone whose role
    // outranks the requester's, per the same rank table CanActionLeaveRequest uses —
    // i.e. exactly the set of people who are actually allowed to action this request.
    private static async Task<List<string>> GetEligibleApproverUserIdsAsync(int requesterEmployeeId, AppDbContext db)
    {
        var requesterRole = await GetEmployeeRoleAsync(requesterEmployeeId, db);
        if (requesterRole is null) return [];
        var requesterRank = GetRoleRank(requesterRole.Value);

        var employeeRoles = await db.Employees
            .Where(e => e.UserId != null && e.Id != requesterEmployeeId)
            .Select(e => new
            {
                UserId = e.UserId!,
                RoleName = db.Roles
                    .Where(r => db.UserRoles.Any(ur => ur.UserId == e.UserId && ur.RoleId == r.Id))
                    .Select(r => r.Name)
                    .FirstOrDefault()
            })
            .ToListAsync();

        return employeeRoles
            .Where(e => e.RoleName is not null && GetRoleRank(Enum.Parse<Role>(e.RoleName)) > requesterRank)
            .Select(e => e.UserId)
            .ToList();
    }

    // Strict linear seniority used for approver routing: Employee < Supervisor < Manager < Admin.
    // Do NOT use (int)role here — the enum's declared order in Models/Enums.cs
    // (Admin=0, Manager=1, Supervisor=2, Employee=3) is the exact reverse of seniority rank.
    private static int GetRoleRank(Role role) => role switch
    {
        Role.Employee => 0,
        Role.Supervisor => 1,
        Role.Manager => 2,
        Role.Admin => 3,
        _ => 0
    };

    private static int GetCallerRoleRank(ClaimsPrincipal principal)
    {
        if (principal.IsInRole(nameof(Role.Admin))) return 3;
        if (principal.IsInRole(nameof(Role.Manager))) return 2;
        if (principal.IsInRole(nameof(Role.Supervisor))) return 1;
        return 0;
    }

    private static bool CanActionLeaveRequest(ClaimsPrincipal principal, Role requesterRole) =>
        GetCallerRoleRank(principal) > GetRoleRank(requesterRole);

    private static LeaveRequestDetailDto ToDetailDto(LeaveRequest l) => new(
        l.Id, l.RequesterId, l.StartDate, l.EndDate, l.Reason, l.Status,
        l.ApproverEmployeeId, l.DecisionReason, l.RequestedDelayDate, l.CreatedAt, l.UpdatedAt);
}
