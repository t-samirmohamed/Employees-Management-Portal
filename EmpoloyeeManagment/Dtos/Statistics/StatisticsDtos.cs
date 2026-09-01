namespace EmpoloyeeManagment.Dtos.Statistics;

public record EmployeeStatisticsDto(
    int TotalEmployees,
    Dictionary<string, int> ByGender,
    Dictionary<string, int> ByStatus,
    Dictionary<string, int> ByAttendanceStatus);

public record EmployeeMonthlyActivityDto(
    int EmployeeId,
    int Year,
    int Month,
    int TotalTasks,
    Dictionary<string, int> TasksByStatus,
    int TotalVisits,
    Dictionary<string, int> VisitsByStatus,
    string CurrentAttendanceStatus,
    int AttendanceDrivingTaskCount);

public record StatusStatsDto(
    Dictionary<string, int> TasksByStatus,
    Dictionary<string, int> VisitsByStatus);

public record MostVisitedClientDto(int ClientId, string ClientName, int VisitCount);

public record SupervisorTeamStatsDto(
    int SupervisorEmployeeId,
    string SupervisorName,
    int? AssignedClientId,
    string? AssignedClientName,
    List<int> TeamEmployeeIds,
    int OpenTaskCount,
    Dictionary<string, int> TeamAttendanceStatusBreakdown);
