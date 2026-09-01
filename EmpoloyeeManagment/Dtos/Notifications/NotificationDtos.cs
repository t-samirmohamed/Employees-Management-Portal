using EmpoloyeeManagment.Models;

namespace EmpoloyeeManagment.Dtos.Notifications;

public record NotificationDto(int Id, NotificationType Type, int ReferenceId, bool IsRead, DateTime CreatedAt);
