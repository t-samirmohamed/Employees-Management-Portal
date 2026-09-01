namespace EmpoloyeeManagment.Models;

public class TaskItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime DueDateTime { get; set; }
    public int AssigneeId { get; set; }
    public int? RelatedVisitId { get; set; }
    public string? Notes { get; set; }
    public bool AttendanceRequired { get; set; }
    public TaskItemStatus Status { get; set; } = TaskItemStatus.New;
    public string? RejectionReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
