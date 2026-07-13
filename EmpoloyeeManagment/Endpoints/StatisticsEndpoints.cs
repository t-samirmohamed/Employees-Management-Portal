using EmpoloyeeManagment.Data;
using EmpoloyeeManagment.Dtos.Statistics;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace EmpoloyeeManagment.Endpoints;

public static class StatisticsEndpoints
{
    public static IEndpointRouteBuilder MapStatisticsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/statistics").WithTags("Statistics").RequireAuthorization();

        group.MapGet("/employees", GetEmployeeStatisticsAsync);

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
}
