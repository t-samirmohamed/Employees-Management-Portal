using EmpoloyeeManagment.Data;
using EmpoloyeeManagment.Models;

namespace EmpoloyeeManagment.Services;

public class NotificationService(AppDbContext db, ILogger<NotificationService> logger) : INotificationService
{
    public Task NotifyLeaveRequestSubmittedAsync(int leaveRequestId, IEnumerable<string> recipientUserIds) =>
        CreateNotificationsAsync(NotificationType.LeaveRequestSubmitted, leaveRequestId, recipientUserIds);

    public Task NotifyLeaveRequestAcceptedAsync(int leaveRequestId, string recipientUserId) =>
        CreateNotificationsAsync(NotificationType.LeaveRequestAccepted, leaveRequestId, [recipientUserId]);

    public Task NotifyLeaveRequestRejectedAsync(int leaveRequestId, string recipientUserId) =>
        CreateNotificationsAsync(NotificationType.LeaveRequestRejected, leaveRequestId, [recipientUserId]);

    public Task NotifyLeaveRequestDelayRequestedAsync(int leaveRequestId, string recipientUserId) =>
        CreateNotificationsAsync(NotificationType.LeaveRequestDelayRequested, leaveRequestId, [recipientUserId]);

    public Task NotifyTaskAssignedAsync(int taskId, string recipientUserId) =>
        CreateNotificationsAsync(NotificationType.TaskAssigned, taskId, [recipientUserId]);

    public Task NotifyVisitAssignedAsync(int visitId, string recipientUserId) =>
        CreateNotificationsAsync(NotificationType.VisitAssigned, visitId, [recipientUserId]);

    private async Task CreateNotificationsAsync(NotificationType type, int referenceId, IEnumerable<string> recipientUserIds)
    {
        try
        {
            var now = DateTime.UtcNow;
            var notifications = recipientUserIds
                .Distinct()
                .Select(userId => new Notification
                {
                    RecipientUserId = userId,
                    Type = type,
                    ReferenceId = referenceId,
                    IsRead = false,
                    CreatedAt = now
                });

            db.Notifications.AddRange(notifications);
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create {NotificationType} notifications for reference {ReferenceId}", type, referenceId);
        }
    }
}
