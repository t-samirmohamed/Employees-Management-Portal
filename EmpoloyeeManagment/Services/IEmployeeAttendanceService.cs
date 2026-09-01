namespace EmpoloyeeManagment.Services;

public interface IEmployeeAttendanceService
{
    Task RecalculateAsync(int employeeId);
}
