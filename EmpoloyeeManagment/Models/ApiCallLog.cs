namespace EmpoloyeeManagment.Models;

public class ApiCallLog
{
    public long Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string? QueryString { get; set; }
    public int StatusCode { get; set; }
    public long DurationMs { get; set; }
    public string? UserId { get; set; }
    public string? IpAddress { get; set; }
}
