using Google.Protobuf.WellKnownTypes;
using TaskBoard.Contracts.V1;
using DomainTaskItem = TaskBoard.Domain.TaskItem;
using DomainTaskItemPriority = TaskBoard.Domain.TaskItemPriority;
using DomainTaskItemStatus = TaskBoard.Domain.TaskItemStatus;

namespace TaskBoard.Application.Mapping;

public static class TaskItemMappingExtensions
{
    public static TaskItem ToProto(this DomainTaskItem task)
    {
        var proto = new TaskItem
        {
            Id = task.Id,
            BoardId = task.BoardId,
            Title = task.Title,
            Description = task.Description,
            Status = task.Status.ToProto(),
            Priority = task.Priority.ToProto(),
            CreatedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(task.CreatedAt, DateTimeKind.Utc)),
            UpdatedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(task.UpdatedAt, DateTimeKind.Utc))
        };

        if (task.DueDate is { } dueDate)
            proto.DueDate = Timestamp.FromDateTime(DateTime.SpecifyKind(dueDate, DateTimeKind.Utc));

        // task.Labels is only populated when the entity was loaded with .Include(t => t.Labels) -
        // otherwise this silently adds nothing, EF Core does not lazy-load by default.
        proto.LabelIds.AddRange(task.Labels.Select(l => l.Id));

        return proto;
    }

    // Only the scalar fields; BoardId existence and LabelIds resolution need DB access
    // and are handled by TaskItemGrpcService, not here.
    public static DomainTaskItem ToDomain(this CreateTaskItemRequest request)
    {
        var now = DateTime.UtcNow;
        return new DomainTaskItem
        {
            BoardId = request.BoardId,
            Title = request.Title,
            Description = request.HasDescription ? request.Description : string.Empty,
            Status = request.Status.ToDomain(),
            Priority = request.Priority.ToDomain(),
            DueDate = request.DueDate?.ToDateTime(),
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    // Proto TaskItemStatus.Todo = 1 (Unspecified = 0 has no Domain counterpart).
    // An unset/Unspecified status falls back to Todo, same as TaskItem's own default.
    public static DomainTaskItemStatus ToDomain(this TaskItemStatus status) => status switch
    {
        TaskItemStatus.Todo => DomainTaskItemStatus.Todo,
        TaskItemStatus.InProgress => DomainTaskItemStatus.InProgress,
        TaskItemStatus.Done => DomainTaskItemStatus.Done,
        _ => DomainTaskItemStatus.Todo
    };

    public static TaskItemStatus ToProto(this DomainTaskItemStatus status) => status switch
    {
        DomainTaskItemStatus.Todo => TaskItemStatus.Todo,
        DomainTaskItemStatus.InProgress => TaskItemStatus.InProgress,
        DomainTaskItemStatus.Done => TaskItemStatus.Done,
        _ => TaskItemStatus.Unspecified
    };

    public static DomainTaskItemPriority ToDomain(this TaskItemPriority priority) => priority switch
    {
        TaskItemPriority.Low => DomainTaskItemPriority.Low,
        TaskItemPriority.Medium => DomainTaskItemPriority.Medium,
        TaskItemPriority.High => DomainTaskItemPriority.High,
        _ => DomainTaskItemPriority.Medium
    };

    public static TaskItemPriority ToProto(this DomainTaskItemPriority priority) => priority switch
    {
        DomainTaskItemPriority.Low => TaskItemPriority.Low,
        DomainTaskItemPriority.Medium => TaskItemPriority.Medium,
        DomainTaskItemPriority.High => TaskItemPriority.High,
        _ => TaskItemPriority.Unspecified
    };
}
