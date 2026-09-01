using EmpoloyeeManagment.Data;
using EmpoloyeeManagment.Models;
using Microsoft.EntityFrameworkCore;

namespace EmpoloyeeManagment.Services;

public class EmployeeAttendanceService(AppDbContext db) : IEmployeeAttendanceService
{
    public async Task RecalculateAsync(int employeeId)
    {
        var employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == employeeId);
        if (employee is null) return;

        // Absent/OnVacation (set by a future leaves feature) takes precedence over
        // task-driven attendance; only ever toggle between InOffice and OutOfOffice.
        if (employee.AttendanceStatus != AttendanceStatus.InOffice && employee.AttendanceStatus != AttendanceStatus.OutOfOffice)
        {
            return;
        }

        var hasActiveAttendanceTask = await db.Tasks
            .AnyAsync(t => t.AssigneeId == employeeId && t.AttendanceRequired && t.Status == TaskItemStatus.InProgress);

        employee.AttendanceStatus = hasActiveAttendanceTask ? AttendanceStatus.OutOfOffice : AttendanceStatus.InOffice;
        await db.SaveChangesAsync();
    }
}
