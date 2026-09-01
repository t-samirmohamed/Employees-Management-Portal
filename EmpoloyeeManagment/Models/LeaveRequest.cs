namespace EmpoloyeeManagment.Models;

public class LeaveRequest
{
    public int Id { get; set; }
    public int RequesterId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Reason { get; set; } = string.Empty;
    public LeaveRequestStatus Status { get; set; } = LeaveRequestStatus.Pending;
    public int? ApproverEmployeeId { get; set; }
    public string? DecisionReason { get; set; }
    public DateTime? RequestedDelayDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
