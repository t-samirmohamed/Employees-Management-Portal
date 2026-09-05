namespace EmpoloyeeManagment.Services;

public interface INotificationService
{
    Task NotifyLeaveRequestSubmittedAsync(int leaveRequestId, IEnumerable<string> recipientUserIds);
    Task NotifyLeaveRequestAcceptedAsync(int leaveRequestId, string recipientUserId);
    Task NotifyLeaveRequestRejectedAsync(int leaveRequestId, string recipientUserId);
    Task NotifyLeaveRequestDelayRequestedAsync(int leaveRequestId, string recipientUserId);
    Task NotifyTaskAssignedAsync(int taskId, string recipientUserId);
    Task NotifyVisitAssignedAsync(int visitId, string recipientUserId);
}
