using Google.Protobuf.WellKnownTypes;
using TaskBoard.Application.Mapping;
using TaskBoard.Contracts.V1;
using DomainLabel = TaskBoard.Domain.Label;
using DomainTaskItem = TaskBoard.Domain.TaskItem;
using DomainTaskItemPriority = TaskBoard.Domain.TaskItemPriority;
using DomainTaskItemStatus = TaskBoard.Domain.TaskItemStatus;

namespace TaskBoard.Api.Tests.Mapping;

public class TaskItemMappingExtensionsTests
{
    [Theory]
    [InlineData(DomainTaskItemStatus.Todo, TaskItemStatus.Todo)]
    [InlineData(DomainTaskItemStatus.InProgress, TaskItemStatus.InProgress)]
    [InlineData(DomainTaskItemStatus.Done, TaskItemStatus.Done)]
    public void Status_RoundTrip_PreservesEveryValue(DomainTaskItemStatus domain, TaskItemStatus proto)
    {
        Assert.Equal(proto, domain.ToProto());
        Assert.Equal(domain, proto.ToDomain());
    }

    [Theory]
    [InlineData(DomainTaskItemPriority.Low, TaskItemPriority.Low)]
    [InlineData(DomainTaskItemPriority.Medium, TaskItemPriority.Medium)]
    [InlineData(DomainTaskItemPriority.High, TaskItemPriority.High)]
    public void Priority_RoundTrip_PreservesEveryValue(DomainTaskItemPriority domain, TaskItemPriority proto)
    {
        Assert.Equal(proto, domain.ToProto());
        Assert.Equal(domain, proto.ToDomain());
    }

    [Fact]
    public void ToDomain_UnspecifiedEnums_FallBackToDefaults()
    {
        Assert.Equal(DomainTaskItemStatus.Todo, TaskItemStatus.Unspecified.ToDomain());
        Assert.Equal(DomainTaskItemPriority.Medium, TaskItemPriority.Unspecified.ToDomain());
    }

    [Fact]
    public void ToProto_UnknownDomainEnums_MapToUnspecified()
    {
        Assert.Equal(TaskItemStatus.Unspecified, ((DomainTaskItemStatus)99).ToProto());
        Assert.Equal(TaskItemPriority.Unspecified, ((DomainTaskItemPriority)99).ToProto());
    }

    [Fact]
    public void ToProto_UnspecifiedKindDates_AreTreatedAsUtc()
    {
        var task = NewTask();
        task.CreatedAt = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Unspecified);
        task.UpdatedAt = new DateTime(2024, 1, 3, 3, 4, 5, DateTimeKind.Unspecified);
        task.DueDate = new DateTime(2024, 2, 1, 0, 0, 0, DateTimeKind.Unspecified);

        var proto = task.ToProto();

        Assert.Equal(new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc), proto.CreatedAt.ToDateTime());
        Assert.Equal(new DateTime(2024, 1, 3, 3, 4, 5, DateTimeKind.Utc), proto.UpdatedAt.ToDateTime());
        Assert.Equal(new DateTime(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc), proto.DueDate.ToDateTime());
    }

    [Fact]
    public void ToProto_NoDueDate_LeavesDueDateUnset()
    {
        var proto = NewTask().ToProto();

        Assert.Null(proto.DueDate);
    }

    [Fact]
    public void ToProto_LabelsLoaded_MapsLabelIds()
    {
        var task = NewTask();
        task.Labels.Add(new DomainLabel { Id = 2, Name = "a", Color = "#000" });
        task.Labels.Add(new DomainLabel { Id = 5, Name = "b", Color = "#000" });

        Assert.Equal(new[] { 2, 5 }, task.ToProto().LabelIds);
    }

    [Fact]
    public void ToProto_LabelsNotLoaded_YieldsEmptyLabelIds()
    {
        // Documents the contract: without .Include(t => t.Labels) the ids are silently absent.
        Assert.Empty(NewTask().ToProto().LabelIds);
    }

    [Fact]
    public void ToDomain_FullRequest_MapsScalarsAndTimestamps()
    {
        var due = new DateTime(2030, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var request = new CreateTaskItemRequest
        {
            BoardId = 7, Title = "T", Description = "D",
            Status = TaskItemStatus.Done, Priority = TaskItemPriority.High,
            DueDate = Timestamp.FromDateTime(due)
        };

        var task = request.ToDomain();

        Assert.Equal(7, task.BoardId);
        Assert.Equal("T", task.Title);
        Assert.Equal("D", task.Description);
        Assert.Equal(DomainTaskItemStatus.Done, task.Status);
        Assert.Equal(DomainTaskItemPriority.High, task.Priority);
        Assert.Equal(due, task.DueDate);
        Assert.Equal(DateTimeKind.Utc, task.DueDate!.Value.Kind);
        Assert.Equal(task.CreatedAt, task.UpdatedAt);
        Assert.Equal(DateTimeKind.Utc, task.CreatedAt.Kind);
    }

    [Fact]
    public void ToDomain_MinimalRequest_UsesDefaults()
    {
        var task = new CreateTaskItemRequest { BoardId = 1, Title = "T" }.ToDomain();

        Assert.Equal(string.Empty, task.Description);
        Assert.Equal(DomainTaskItemStatus.Todo, task.Status);
        Assert.Equal(DomainTaskItemPriority.Medium, task.Priority);
        Assert.Null(task.DueDate);
    }

    private static DomainTaskItem NewTask() => new()
    {
        Id = 1, BoardId = 1, Title = "T",
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };
}
