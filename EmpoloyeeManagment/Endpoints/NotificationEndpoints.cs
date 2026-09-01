using System.Security.Claims;
using EmpoloyeeManagment.Data;
using EmpoloyeeManagment.Dtos.Notifications;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace EmpoloyeeManagment.Endpoints;

public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/notifications").WithTags("Notifications").RequireAuthorization();

        group.MapGet("/", GetNotificationsAsync);
        group.MapPatch("/{id:int}/read", MarkNotificationReadAsync);

        return app;
    }

    private static async Task<Ok<List<NotificationDto>>> GetNotificationsAsync(ClaimsPrincipal principal, AppDbContext db)
    {
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return TypedResults.Ok(new List<NotificationDto>());

        var notifications = await db.Notifications.AsNoTracking()
            .Where(n => n.RecipientUserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new NotificationDto(n.Id, n.Type, n.ReferenceId, n.IsRead, n.CreatedAt))
            .ToListAsync();

        return TypedResults.Ok(notifications);
    }

    private static async Task<Results<Ok<NotificationDto>, NotFound>> MarkNotificationReadAsync(
        int id, ClaimsPrincipal principal, AppDbContext db)
    {
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);

        var notification = await db.Notifications.FirstOrDefaultAsync(n => n.Id == id);
        if (notification is null || userId is null || notification.RecipientUserId != userId)
        {
            return TypedResults.NotFound();
        }

        notification.IsRead = true;
        await db.SaveChangesAsync();

        return TypedResults.Ok(new NotificationDto(notification.Id, notification.Type, notification.ReferenceId, notification.IsRead, notification.CreatedAt));
    }
}
