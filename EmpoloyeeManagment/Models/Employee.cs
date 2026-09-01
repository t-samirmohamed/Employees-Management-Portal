namespace EmpoloyeeManagment.Models;

public class Employee
{
    public int Id { get; set; }
    public string? UserId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public Gender Gender { get; set; }
    public EmployeeStatus Status { get; set; } = EmployeeStatus.Active;
    public AttendanceStatus AttendanceStatus { get; set; } = AttendanceStatus.InOffice;
    public int? AssignedLocationId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
