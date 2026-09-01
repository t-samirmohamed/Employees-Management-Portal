using EmpoloyeeManagment.Models;

namespace EmpoloyeeManagment.Dtos.Users;

public record CreateUserRequest(string Email, string Password, string FirstName, string LastName, Gender Gender, Role Role, int? AssignedLocationId);

public record UserResponse(string Id, string Email, Role Role);

public record EmployeeSummaryDto(int Id, string FirstName, string LastName, EmployeeStatus Status, int? AssignedLocationId);

public record UserListItemDto(string Id, string Email, Role Role, EmployeeSummaryDto? Employee);
