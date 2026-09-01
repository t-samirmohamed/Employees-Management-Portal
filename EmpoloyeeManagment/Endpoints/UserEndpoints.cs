using EmpoloyeeManagment.Data;
using EmpoloyeeManagment.Dtos.Users;
using EmpoloyeeManagment.Models;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EmpoloyeeManagment.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users").WithTags("Users").RequireAuthorization("AdminOnly");

        group.MapGet("/", GetUsersAsync);
        group.MapPost("/", CreateUserAsync);

        return app;
    }

    private static async Task<Ok<List<UserListItemDto>>> GetUsersAsync(AppDbContext db)
    {
        var raw = await db.Users
            .AsNoTracking()
            .Select(u => new
            {
                u.Id,
                Email = u.Email ?? string.Empty,
                RoleName = db.Roles
                    .Where(r => db.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleId == r.Id))
                    .Select(r => r.Name)
                    .FirstOrDefault(),
                Employee = db.Employees
                    .Where(e => e.UserId == u.Id)
                    .Select(e => new EmployeeSummaryDto(e.Id, e.FirstName, e.LastName, e.Status, e.AssignedLocationId))
                    .FirstOrDefault()
            })
            .ToListAsync();

        var users = raw
            .Select(u => new UserListItemDto(u.Id, u.Email, Enum.Parse<Role>(u.RoleName!), u.Employee))
            .ToList();

        return TypedResults.Ok(users);
    }

    private static async Task<Results<Created<UserResponse>, ValidationProblem, NotFound>> CreateUserAsync(
        CreateUserRequest request,
        UserManager<ApplicationUser> userManager,
        AppDbContext db)
    {
        if (request.AssignedLocationId is not null)
        {
            var locationExists = await db.Locations.AnyAsync(l => l.Id == request.AssignedLocationId);
            if (!locationExists) return TypedResults.NotFound();
        }

        await using var transaction = await db.Database.BeginTransactionAsync();

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            var errors = result.Errors
                .GroupBy(e => e.Code)
                .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());
            return TypedResults.ValidationProblem(errors);
        }

        await userManager.AddToRoleAsync(user, request.Role.ToString());

        db.Employees.Add(new Employee
        {
            UserId = user.Id,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            Gender = request.Gender,
            Status = EmployeeStatus.Inactive,
            AttendanceStatus = AttendanceStatus.OutOfOffice,
            AssignedLocationId = request.AssignedLocationId,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        return TypedResults.Created($"/api/users/{user.Id}", new UserResponse(user.Id, user.Email ?? string.Empty, request.Role));
    }
}
