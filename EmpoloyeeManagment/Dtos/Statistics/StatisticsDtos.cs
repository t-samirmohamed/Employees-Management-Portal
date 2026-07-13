namespace EmpoloyeeManagment.Dtos.Statistics;

public record EmployeeStatisticsDto(
    int TotalEmployees,
    Dictionary<string, int> ByGender,
    Dictionary<string, int> ByStatus,
    Dictionary<string, int> ByAttendanceStatus);
