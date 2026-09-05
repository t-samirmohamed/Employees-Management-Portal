namespace EmpoloyeeManagment.Models;

public enum Gender
{
    M,
    F
}

public enum EmployeeStatus
{
    Active,
    Inactive
}

public enum AttendanceStatus
{
    InOffice,
    Absent,
    OnVacation,
    OutOfOffice
}

public enum Role
{
    Admin,
    Manager,
    Supervisor,
    Employee
}

public enum TaskItemStatus
{
    New,
    InProgress,
    Rejected,
    Cancelled,
    Done
}

public enum LeaveRequestStatus
{
    Pending,
    Accepted,
    Rejected,
    DelayRequested
}

public enum NotificationType
{
    LeaveRequestSubmitted,
    LeaveRequestAccepted,
    LeaveRequestRejected,
    LeaveRequestDelayRequested,
    TaskAssigned,
    VisitAssigned
}
