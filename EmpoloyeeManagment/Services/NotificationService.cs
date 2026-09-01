using EmpoloyeeManagment.Data;
using EmpoloyeeManagment.Models;

namespace EmpoloyeeManagment.Services;

public class NotificationService(AppDbContext db, ILogger<NotificationService> logger) : INotificationService
{
    public async Task NotifyLeaveRequestSubmittedAsync(int leaveRequestId, IEnumerable<string> recipientUserIds)
    {
        try
        {
            var now = DateTime.UtcNow;
            var notifications = recipientUserIds
                .Distinct()
                .Select(userId => new Notification
                {
                    RecipientUserId = userId,
                    Type = NotificationType.LeaveRequestSubmitted,
                    ReferenceId = leaveRequestId,
                    IsRead = false,
                    CreatedAt = now
                });

            db.Notifications.AddRange(notifications);
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create leave-request-submitted notifications for leave request {LeaveRequestId}", leaveRequestId);
        }
    }
}
