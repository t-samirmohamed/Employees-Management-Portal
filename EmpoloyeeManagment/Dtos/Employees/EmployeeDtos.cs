using EmpoloyeeManagment.Models;

namespace EmpoloyeeManagment.Dtos.Employees;

public record CreateEmployeeRequest(
    string FirstName,
    string LastName,
    string Email,
    Gender Gender,
    AttendanceStatus? AttendanceStatus);

public record EmployeeListItemDto(
    int Id,
    string FirstName,
    string LastName,
    string Email,
    Gender Gender,
    EmployeeStatus Status,
    AttendanceStatus AttendanceStatus);

public record EmployeeDetailDto(
    int Id,
    string FirstName,
    string LastName,
    string Email,
    Gender Gender,
    EmployeeStatus Status,
    AttendanceStatus AttendanceStatus,
    DateTime CreatedAt);
