namespace EmpoloyeeManagment.Models;

public class TaskComment
{
    public int Id { get; set; }
    public int TaskId { get; set; }
    public int AuthorEmployeeId { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
