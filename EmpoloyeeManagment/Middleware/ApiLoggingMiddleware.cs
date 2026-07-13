using System.Diagnostics;
using System.Security.Claims;
using EmpoloyeeManagment.Data;
using EmpoloyeeManagment.Models;

namespace EmpoloyeeManagment.Middleware;

public class ApiLoggingMiddleware(RequestDelegate next, ILogger<ApiLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, AppDbContext db)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await next(context);
        }
        finally
        {
            stopwatch.Stop();

            try
            {
                db.ApiCallLogs.Add(new ApiCallLog
                {
                    Timestamp = DateTime.UtcNow,
                    Method = context.Request.Method,
                    Path = context.Request.Path.Value ?? string.Empty,
                    QueryString = context.Request.QueryString.HasValue ? context.Request.QueryString.Value : null,
                    StatusCode = context.Response.StatusCode,
                    DurationMs = stopwatch.ElapsedMilliseconds,
                    UserId = context.User.FindFirstValue(ClaimTypes.NameIdentifier),
                    IpAddress = context.Connection.RemoteIpAddress?.ToString()
                });
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to write API call log for {Method} {Path}", context.Request.Method, context.Request.Path);
            }
        }
    }
}
