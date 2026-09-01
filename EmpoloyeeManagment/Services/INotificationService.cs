namespace EmpoloyeeManagment.Services;

public interface INotificationService
{
    Task NotifyLeaveRequestSubmittedAsync(int leaveRequestId, IEnumerable<string> recipientUserIds);
}
