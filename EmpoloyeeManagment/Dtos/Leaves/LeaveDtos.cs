using EmpoloyeeManagment.Models;

namespace EmpoloyeeManagment.Dtos.Leaves;

public record CreateLeaveRequestRequest(DateTime StartDate, DateTime EndDate, string Reason);

public record UpdateLeaveRequestRequest(DateTime StartDate, DateTime EndDate, string Reason);

public record RejectLeaveRequestRequest(string Reason);

public record RequestDelayRequest(DateTime TargetDate, string Reason);

public record LeaveRequestListItemDto(
    int Id,
    int RequesterId,
    DateTime StartDate,
    DateTime EndDate,
    LeaveRequestStatus Status,
    int? ApproverEmployeeId);

public record LeaveRequestDetailDto(
    int Id,
    int RequesterId,
    DateTime StartDate,
    DateTime EndDate,
    string Reason,
    LeaveRequestStatus Status,
    int? ApproverEmployeeId,
    string? DecisionReason,
    DateTime? RequestedDelayDate,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
