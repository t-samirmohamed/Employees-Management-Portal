using EmpoloyeeManagment.Data;
using EmpoloyeeManagment.Dtos.Employees;
using EmpoloyeeManagment.Models;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace EmpoloyeeManagment.Endpoints;

public static class EmployeeEndpoints
{
    public static IEndpointRouteBuilder MapEmployeeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/employees").WithTags("Employees").RequireAuthorization();

        group.MapGet("/", GetEmployeesAsync);
        group.MapGet("/{id:int}", GetEmployeeByIdAsync);
        group.MapPost("/", CreateEmployeeAsync);
        group.MapPatch("/{id:int}/activate", ActivateEmployeeAsync).RequireAuthorization("AdminOnly");
        group.MapPatch("/{id:int}/deactivate", DeactivateEmployeeAsync).RequireAuthorization("AdminOnly");

        return app;
    }

    private static async Task<Ok<List<EmployeeListItemDto>>> GetEmployeesAsync(
        AppDbContext db,
        Gender? gender,
        EmployeeStatus? status,
        AttendanceStatus? attendanceStatus)
    {
        var query = db.Employees.AsNoTracking().AsQueryable();

        if (gender is not null) query = query.Where(e => e.Gender == gender);
        if (status is not null) query = query.Where(e => e.Status == status);
        if (attendanceStatus is not null) query = query.Where(e => e.AttendanceStatus == attendanceStatus);

        var employees = await query
            .OrderBy(e => e.LastName)
            .Select(e => new EmployeeListItemDto(e.Id, e.FirstName, e.LastName, e.Email, e.Gender, e.Status, e.AttendanceStatus))
            .ToListAsync();

        return TypedResults.Ok(employees);
    }

    private static async Task<Results<Ok<EmployeeDetailDto>, NotFound>> GetEmployeeByIdAsync(int id, AppDbContext db)
    {
        var employee = await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);
        return employee is null ? TypedResults.NotFound() : TypedResults.Ok(ToDetailDto(employee));
    }

    private static async Task<Created<EmployeeDetailDto>> CreateEmployeeAsync(CreateEmployeeRequest request, AppDbContext db)
    {
        var employee = new Employee
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            Gender = request.Gender,
            Status = EmployeeStatus.Active,
            AttendanceStatus = request.AttendanceStatus ?? AttendanceStatus.InOffice,
            CreatedAt = DateTime.UtcNow
        };

        db.Employees.Add(employee);
        await db.SaveChangesAsync();

        return TypedResults.Created($"/api/employees/{employee.Id}", ToDetailDto(employee));
    }

    private static Task<Results<Ok<EmployeeDetailDto>, NotFound>> ActivateEmployeeAsync(int id, AppDbContext db) =>
        SetStatusAsync(id, db, EmployeeStatus.Active);

    private static Task<Results<Ok<EmployeeDetailDto>, NotFound>> DeactivateEmployeeAsync(int id, AppDbContext db) =>
        SetStatusAsync(id, db, EmployeeStatus.Inactive);

    private static async Task<Results<Ok<EmployeeDetailDto>, NotFound>> SetStatusAsync(int id, AppDbContext db, EmployeeStatus status)
    {
        var employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == id);
        if (employee is null) return TypedResults.NotFound();

        employee.Status = status;
        await db.SaveChangesAsync();

        return TypedResults.Ok(ToDetailDto(employee));
    }

    private static EmployeeDetailDto ToDetailDto(Employee employee) => new(
        employee.Id,
        employee.FirstName,
        employee.LastName,
        employee.Email,
        employee.Gender,
        employee.Status,
        employee.AttendanceStatus,
        employee.CreatedAt);
}
