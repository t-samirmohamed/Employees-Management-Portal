namespace EmpoloyeeManagment.Models;

public class Visit
{
    public int Id { get; set; }
    public DateTime DateTime { get; set; }
    public int ClientId { get; set; }
    public int LocationId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
