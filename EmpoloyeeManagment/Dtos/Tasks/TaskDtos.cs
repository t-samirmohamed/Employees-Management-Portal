using EmpoloyeeManagment.Models;

namespace EmpoloyeeManagment.Dtos.Tasks;

public record CreateTaskRequest(
    string Name,
    DateTime DueDateTime,
    int AssigneeId,
    int? RelatedVisitId,
    string? Notes,
    bool AttendanceRequired);

public record ReassignTaskRequest(int NewAssigneeId);

public record RejectTaskRequest(string Reason);

public record AddCommentRequest(string Text);

public record UpdateCommentRequest(string Text);

public record TaskListItemDto(
    int Id,
    string Name,
    DateTime DueDateTime,
    int AssigneeId,
    int? RelatedVisitId,
    TaskItemStatus Status,
    bool AttendanceRequired);

public record TaskDetailDto(
    int Id,
    string Name,
    DateTime DueDateTime,
    int AssigneeId,
    int? RelatedVisitId,
    string? Notes,
    bool AttendanceRequired,
    TaskItemStatus Status,
    string? RejectionReason,
    DateTime CreatedAt);

public record TaskCommentDto(int Id, int AuthorEmployeeId, string Text, DateTime CreatedAt, DateTime? UpdatedAt);

public record TaskDetailResponse(TaskDetailDto Task, List<TaskCommentDto> Comments);
