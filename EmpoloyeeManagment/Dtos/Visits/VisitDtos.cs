using EmpoloyeeManagment.Models;

namespace EmpoloyeeManagment.Dtos.Visits;

public record CreateVisitRequest(
    DateTime DateTime,
    int ClientId,
    int LocationId,
    int AssigneeId,
    string? Name,
    string? Notes);

public record VisitListItemDto(
    int Id,
    DateTime DateTime,
    int ClientId,
    int LocationId,
    int AssigneeId,
    TaskItemStatus Status,
    int TaskId);

public record VisitDetailDto(
    int Id,
    DateTime DateTime,
    int ClientId,
    int LocationId,
    int AssigneeId,
    TaskItemStatus Status,
    int TaskId,
    DateTime CreatedAt);
