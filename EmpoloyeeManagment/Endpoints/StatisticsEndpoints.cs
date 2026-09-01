using System.Security.Claims;
using EmpoloyeeManagment.Data;
using EmpoloyeeManagment.Dtos.Statistics;
using EmpoloyeeManagment.Models;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace EmpoloyeeManagment.Endpoints;

public static class StatisticsEndpoints
{
    public static IEndpointRouteBuilder MapStatisticsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/statistics").WithTags("Statistics").RequireAuthorization();

        group.MapGet("/employees", GetEmployeeStatisticsAsync);
        group.MapGet("/employees/{id:int}/monthly", GetEmployeeMonthlyActivityAsync).RequireAuthorization("TaskAssigner");
        group.MapGet("/status", GetStatusStatisticsAsync).RequireAuthorization("TaskAssigner");
        group.MapGet("/clients/most-visited", GetMostVisitedClientsAsync).RequireAuthorization("ClientManager");
        group.MapGet("/supervisors", GetSupervisorTeamStatisticsAsync).RequireAuthorization("ClientManager");

        return app;
    }

    private static async Task<Ok<EmployeeStatisticsDto>> GetEmployeeStatisticsAsync(AppDbContext db)
    {
        var total = await db.Employees.CountAsync();

        var byGender = await db.Employees
            .GroupBy(e => e.Gender)
            .Select(x => new { x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Key.ToString(), x => x.Count);

        var byStatus = await db.Employees
            .GroupBy(e => e.Status)
            .Select(x => new { x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Key.ToString(), x => x.Count);

        var byAttendanceStatus = await db.Employees
            .GroupBy(e => e.AttendanceStatus)
            .Select(x => new { x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Key.ToString(), x => x.Count);

        return TypedResults.Ok(new EmployeeStatisticsDto(total, byGender, byStatus, byAttendanceStatus));
    }

    private static async Task<Results<Ok<EmployeeMonthlyActivityDto>, NotFound, BadRequest<string>>> GetEmployeeMonthlyActivityAsync(
        int id, int year, int month, ClaimsPrincipal principal, AppDbContext db)
    {
        if (month < 1 || month > 12)
        {
            return TypedResults.BadRequest("month must be between 1 and 12.");
        }

        var employee = await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);
        if (employee is null) return TypedResults.NotFound();

        if (!await CanViewEmployeeStatsAsync(id, principal, db)) return TypedResults.NotFound();

        var monthStart = new DateTime(year, month, 1);
        var monthEnd = monthStart.AddMonths(1);

        var taskQuery = db.Tasks.AsNoTracking()
            .Where(t => t.AssigneeId == id && t.DueDateTime >= monthStart && t.DueDateTime < monthEnd);

        var totalTasks = await taskQuery.CountAsync();
        var tasksByStatus = await taskQuery
            .GroupBy(t => t.Status)
            .Select(x => new { x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Key.ToString(), x => x.Count);
        var attendanceDrivingTaskCount = await taskQuery.CountAsync(t => t.AttendanceRequired);

        var visitTaskQuery = db.Visits.AsNoTracking()
            .Where(v => v.DateTime >= monthStart && v.DateTime < monthEnd)
            .Join(db.Tasks.AsNoTracking(), v => v.Id, t => t.RelatedVisitId, (v, t) => t)
            .Where(t => t.AssigneeId == id);

        var totalVisits = await visitTaskQuery.CountAsync();
        var visitsByStatus = await visitTaskQuery
            .GroupBy(t => t.Status)
            .Select(x => new { x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Key.ToString(), x => x.Count);

        var dto = new EmployeeMonthlyActivityDto(
            employee.Id, year, month,
            totalTasks, tasksByStatus,
            totalVisits, visitsByStatus,
            employee.AttendanceStatus.ToString(), attendanceDrivingTaskCount);

        return TypedResults.Ok(dto);
    }

    private static async Task<Ok<StatusStatsDto>> GetStatusStatisticsAsync(ClaimsPrincipal principal, AppDbContext db)
    {
        List<int>? teamEmployeeIds = HasFullEmployeeStatsAccess(principal)
            ? null
            : await GetSupervisorTeamEmployeeIdsAsync(principal, db);

        var taskQuery = db.Tasks.AsNoTracking().AsQueryable();
        var visitTaskQuery = db.Visits.AsNoTracking()
            .Join(db.Tasks.AsNoTracking(), v => v.Id, t => t.RelatedVisitId, (v, t) => t);

        if (teamEmployeeIds is not null)
        {
            taskQuery = taskQuery.Where(t => teamEmployeeIds.Contains(t.AssigneeId));
            visitTaskQuery = visitTaskQuery.Where(t => teamEmployeeIds.Contains(t.AssigneeId));
        }

        var tasksByStatus = await taskQuery
            .GroupBy(t => t.Status)
            .Select(x => new { x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Key.ToString(), x => x.Count);

        var visitsByStatus = await visitTaskQuery
            .GroupBy(t => t.Status)
            .Select(x => new { x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Key.ToString(), x => x.Count);

        return TypedResults.Ok(new StatusStatsDto(tasksByStatus, visitsByStatus));
    }

    private static async Task<Results<Ok<List<MostVisitedClientDto>>, BadRequest<string>>> GetMostVisitedClientsAsync(
        string range, int? top, AppDbContext db)
    {
        var cutoff = GetRangeCutoff(range);
        if (cutoff is null)
        {
            return TypedResults.BadRequest("range must be one of: month, 3month, 6month, year.");
        }

        var take = Math.Clamp(top ?? 10, 1, 100);

        var ranked = await db.Visits.AsNoTracking()
            .Where(v => v.DateTime >= cutoff)
            .GroupBy(v => v.ClientId)
            .Select(g => new { ClientId = g.Key, VisitCount = g.Count() })
            .OrderByDescending(x => x.VisitCount)
            .Take(take)
            .ToListAsync();

        var rankedClientIds = ranked.Select(r => r.ClientId).ToList();
        var clientNames = await db.Clients.AsNoTracking()
            .Where(c => rankedClientIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name);

        var result = ranked
            .Select(r => new MostVisitedClientDto(r.ClientId, clientNames.GetValueOrDefault(r.ClientId, "Unknown"), r.VisitCount))
            .ToList();

        return TypedResults.Ok(result);
    }

    private static async Task<Ok<List<SupervisorTeamStatsDto>>> GetSupervisorTeamStatisticsAsync(AppDbContext db)
    {
        var supervisorRoleId = await db.Roles
            .Where(r => r.Name == nameof(Role.Supervisor))
            .Select(r => r.Id)
            .FirstOrDefaultAsync();

        var supervisors = await db.Employees.AsNoTracking()
            .Where(e => e.UserId != null && db.UserRoles.Any(ur => ur.UserId == e.UserId && ur.RoleId == supervisorRoleId))
            .Select(e => new { e.Id, e.FirstName, e.LastName, e.AssignedLocationId })
            .ToListAsync();

        var locationToClient = await db.Locations.AsNoTracking().ToDictionaryAsync(l => l.Id, l => l.ClientId);
        var clientNames = await db.Clients.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Name);

        // Every visit-linked task's assignee, keyed by the visit's ClientId — the "team" roster source.
        var visitAssigneesByClient = await db.Visits.AsNoTracking()
            .Join(db.Tasks.AsNoTracking(), v => v.Id, t => t.RelatedVisitId, (v, t) => new { v.ClientId, t.AssigneeId })
            .ToListAsync();

        var openTaskCountsByAssignee = await db.Tasks.AsNoTracking()
            .Where(t => t.Status == TaskItemStatus.New || t.Status == TaskItemStatus.InProgress)
            .GroupBy(t => t.AssigneeId)
            .Select(g => new { AssigneeId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.AssigneeId, x => x.Count);

        var attendanceByEmployee = await db.Employees.AsNoTracking().ToDictionaryAsync(e => e.Id, e => e.AttendanceStatus);

        var result = new List<SupervisorTeamStatsDto>();
        foreach (var supervisor in supervisors)
        {
            int? clientId = supervisor.AssignedLocationId is not null && locationToClient.TryGetValue(supervisor.AssignedLocationId.Value, out var cid)
                ? cid
                : null;
            var clientName = clientId is not null ? clientNames.GetValueOrDefault(clientId.Value) : null;

            var teamEmployeeIds = clientId is null
                ? []
                : visitAssigneesByClient.Where(x => x.ClientId == clientId).Select(x => x.AssigneeId).Distinct().ToList();

            var openTaskCount = teamEmployeeIds.Sum(eid => openTaskCountsByAssignee.GetValueOrDefault(eid));

            var attendanceBreakdown = teamEmployeeIds
                .Select(eid => attendanceByEmployee.GetValueOrDefault(eid))
                .GroupBy(status => status)
                .ToDictionary(g => g.Key.ToString(), g => g.Count());

            result.Add(new SupervisorTeamStatsDto(
                supervisor.Id, $"{supervisor.FirstName} {supervisor.LastName}",
                clientId, clientName, teamEmployeeIds, openTaskCount, attendanceBreakdown));
        }

        return TypedResults.Ok(result);
    }

    private static bool HasFullEmployeeStatsAccess(ClaimsPrincipal principal) =>
        principal.IsInRole(nameof(Role.Admin)) || principal.IsInRole(nameof(Role.Manager));

    private static async Task<bool> CanViewEmployeeStatsAsync(int employeeId, ClaimsPrincipal principal, AppDbContext db)
    {
        if (HasFullEmployeeStatsAccess(principal)) return true;
        var teamEmployeeIds = await GetSupervisorTeamEmployeeIdsAsync(principal, db);
        return teamEmployeeIds.Contains(employeeId);
    }

    private static async Task<List<int>> GetSupervisorTeamEmployeeIdsAsync(ClaimsPrincipal principal, AppDbContext db)
    {
        var supervisorClientId = await GetSupervisorAssignedClientIdAsync(principal, db);
        if (supervisorClientId is null) return [];

        return await db.Visits.AsNoTracking()
            .Where(v => v.ClientId == supervisorClientId)
            .Join(db.Tasks.AsNoTracking(), v => v.Id, t => t.RelatedVisitId, (v, t) => t.AssigneeId)
            .Distinct()
            .ToListAsync();
    }

    private static async Task<int?> GetSupervisorAssignedClientIdAsync(ClaimsPrincipal principal, AppDbContext db)
    {
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return null;

        var assignedLocationId = await db.Employees
            .Where(e => e.UserId == userId)
            .Select(e => e.AssignedLocationId)
            .FirstOrDefaultAsync();

        if (assignedLocationId is null) return null;

        return await db.Locations
            .Where(l => l.Id == assignedLocationId)
            .Select(l => (int?)l.ClientId)
            .FirstOrDefaultAsync();
    }

    private static DateTime? GetRangeCutoff(string range) => range switch
    {
        "month" => DateTime.UtcNow.AddMonths(-1),
        "3month" => DateTime.UtcNow.AddMonths(-3),
        "6month" => DateTime.UtcNow.AddMonths(-6),
        "year" => DateTime.UtcNow.AddYears(-1),
        _ => null
    };
}
