using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using TaskBoard.Contracts.V1;

namespace TaskBoard.Api.Tests.Services;

public class TaskItemGrpcServiceTests : GrpcTestBase
{
    private readonly TaskItemService.TaskItemServiceClient _tasks;
    private readonly BoardService.BoardServiceClient _boards;
    private readonly LabelService.LabelServiceClient _labels;

    public TaskItemGrpcServiceTests()
    {
        _tasks = new TaskItemService.TaskItemServiceClient(Channel);
        _boards = new BoardService.BoardServiceClient(Channel);
        _labels = new LabelService.LabelServiceClient(Channel);
    }

    private async Task<int> NewBoardId() =>
        (await _boards.CreateBoardAsync(new CreateBoardRequest { Name = "b" })).Id;

    private async Task<int> NewLabelId(string name = "l") =>
        (await _labels.CreateLabelAsync(new CreateLabelRequest { Name = name, Color = "#000000" })).Id;

    private static UpdateTaskItemRequest Update(int id, TaskItem values, params string[] paths) =>
        new() { Id = id, TaskItem = values, UpdateMask = new FieldMask { Paths = { paths } } };

    [Fact]
    public async Task CreateTaskItem_WithLabelsAndDueDate_ReturnsFullyMappedTask()
    {
        var boardId = await NewBoardId();
        var labelId = await NewLabelId();
        var due = new DateTime(2031, 3, 4, 5, 6, 7, DateTimeKind.Utc);

        var created = await _tasks.CreateTaskItemAsync(new CreateTaskItemRequest
        {
            BoardId = boardId, Title = "T", Description = "D",
            Status = TaskItemStatus.InProgress, Priority = TaskItemPriority.High,
            DueDate = Timestamp.FromDateTime(due), LabelIds = { labelId }
        });

        Assert.True(created.Id > 0);
        Assert.Equal(boardId, created.BoardId);
        Assert.Equal(TaskItemStatus.InProgress, created.Status);
        Assert.Equal(TaskItemPriority.High, created.Priority);
        Assert.Equal(due, created.DueDate.ToDateTime());
        Assert.Equal(new[] { labelId }, created.LabelIds);

        var fetched = await _tasks.GetTaskItemAsync(new GetTaskItemRequest { Id = created.Id });
        Assert.Equal(new[] { labelId }, fetched.LabelIds);
        Assert.Equal(due, fetched.DueDate.ToDateTime());
    }

    [Fact]
    public async Task CreateTaskItem_NoOptionalFields_AppliesDefaults()
    {
        var created = await _tasks.CreateTaskItemAsync(new CreateTaskItemRequest { BoardId = await NewBoardId(), Title = "T" });

        Assert.Equal(TaskItemStatus.Todo, created.Status);
        Assert.Equal(TaskItemPriority.Medium, created.Priority);
        Assert.Null(created.DueDate);
        Assert.Empty(created.LabelIds);
    }

    [Fact]
    public async Task CreateTaskItem_UnknownBoard_ReturnsNotFound()
    {
        await AssertRpcError(() => _tasks.CreateTaskItemAsync(new CreateTaskItemRequest { BoardId = 999, Title = "T" }).ResponseAsync, StatusCode.NotFound);
    }

    [Fact]
    public async Task CreateTaskItem_UnknownLabelId_ReturnsNotFoundNamingOnlyTheMissingId()
    {
        var known = await NewLabelId();
        var boardId = await NewBoardId();

        var ex = await AssertRpcError(() => _tasks.CreateTaskItemAsync(new CreateTaskItemRequest
        {
            BoardId = boardId, Title = "T", LabelIds = { known, 999 }
        }).ResponseAsync, StatusCode.NotFound);

        Assert.Equal("Label(s) 999 not found.", ex.Status.Detail);
    }

    [Fact]
    public async Task UpdateTaskItem_UnknownLabelIdInMask_ReturnsNotFoundAndKeepsLabels()
    {
        var known = await NewLabelId();
        var created = await _tasks.CreateTaskItemAsync(new CreateTaskItemRequest
        {
            BoardId = await NewBoardId(), Title = "T", LabelIds = { known }
        });

        await AssertRpcError(() => _tasks.UpdateTaskItemAsync(Update(created.Id, new TaskItem { LabelIds = { 999 } }, "label_ids")).ResponseAsync, StatusCode.NotFound);

        Assert.Equal(new[] { known }, (await _tasks.GetTaskItemAsync(new GetTaskItemRequest { Id = created.Id })).LabelIds);
    }

    [Fact]
    public async Task CreateTaskItem_DuplicateLabelIds_AreAcceptedOnce()
    {
        var known = await NewLabelId();

        var created = await _tasks.CreateTaskItemAsync(new CreateTaskItemRequest
        {
            BoardId = await NewBoardId(), Title = "T", LabelIds = { known, known }
        });

        Assert.Equal(new[] { known }, created.LabelIds);
    }

    [Fact]
    public async Task GetTaskItem_UnknownId_ReturnsNotFound()
    {
        await AssertRpcError(() => _tasks.GetTaskItemAsync(new GetTaskItemRequest { Id = 999 }).ResponseAsync, StatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateTaskItem_MaskListsOnlyTitle_IgnoresOtherFieldsInPayload()
    {
        var created = await _tasks.CreateTaskItemAsync(new CreateTaskItemRequest
        {
            BoardId = await NewBoardId(), Title = "old", Description = "keep", Priority = TaskItemPriority.Low
        });

        var updated = await _tasks.UpdateTaskItemAsync(Update(created.Id,
            new TaskItem { Title = "new", Description = "IGNORED", Priority = TaskItemPriority.High }, "title"));

        Assert.Equal("new", updated.Title);
        Assert.Equal("keep", updated.Description);
        Assert.Equal(TaskItemPriority.Low, updated.Priority);
        Assert.True(updated.UpdatedAt.ToDateTime() >= created.UpdatedAt.ToDateTime());
    }

    [Fact]
    public async Task UpdateTaskItem_MaskWithStatusPriorityDescription_AppliesEach()
    {
        var created = await _tasks.CreateTaskItemAsync(new CreateTaskItemRequest { BoardId = await NewBoardId(), Title = "T" });

        var updated = await _tasks.UpdateTaskItemAsync(Update(created.Id,
            new TaskItem { Description = "D2", Status = TaskItemStatus.Done, Priority = TaskItemPriority.Low },
            "description", "status", "priority"));

        Assert.Equal("D2", updated.Description);
        Assert.Equal(TaskItemStatus.Done, updated.Status);
        Assert.Equal(TaskItemPriority.Low, updated.Priority);
    }

    [Fact]
    public async Task UpdateTaskItem_DueDateInMaskButUnset_ClearsDueDate()
    {
        var created = await _tasks.CreateTaskItemAsync(new CreateTaskItemRequest
        {
            BoardId = await NewBoardId(), Title = "T", DueDate = Timestamp.FromDateTime(DateTime.UtcNow.AddDays(1))
        });

        var updated = await _tasks.UpdateTaskItemAsync(Update(created.Id, new TaskItem(), "due_date"));

        Assert.Null(updated.DueDate);
    }

    [Fact]
    public async Task UpdateTaskItem_LabelIdsInMask_ReplacesLabelSet()
    {
        var l1 = await NewLabelId("one");
        var l2 = await NewLabelId("two");
        var created = await _tasks.CreateTaskItemAsync(new CreateTaskItemRequest
        {
            BoardId = await NewBoardId(), Title = "T", LabelIds = { l1 }
        });

        var updated = await _tasks.UpdateTaskItemAsync(Update(created.Id, new TaskItem { LabelIds = { l2 } }, "label_ids"));

        Assert.Equal(new[] { l2 }, updated.LabelIds);
        Assert.Equal(new[] { l2 }, (await _tasks.GetTaskItemAsync(new GetTaskItemRequest { Id = created.Id })).LabelIds);
    }

    [Fact]
    public async Task UpdateTaskItem_EmptyMask_ReturnsInvalidArgument()
    {
        var created = await _tasks.CreateTaskItemAsync(new CreateTaskItemRequest { BoardId = await NewBoardId(), Title = "T" });

        await AssertRpcError(() => _tasks.UpdateTaskItemAsync(Update(created.Id, new TaskItem { Title = "x" })).ResponseAsync, StatusCode.InvalidArgument);
    }

    [Fact]
    public async Task UpdateTaskItem_UnknownMaskPath_ReturnsInvalidArgument()
    {
        var created = await _tasks.CreateTaskItemAsync(new CreateTaskItemRequest { BoardId = await NewBoardId(), Title = "T" });

        await AssertRpcError(() => _tasks.UpdateTaskItemAsync(Update(created.Id, new TaskItem(), "bogus")).ResponseAsync, StatusCode.InvalidArgument);
    }

    [Fact]
    public async Task UpdateTaskItem_UnknownId_ReturnsNotFound()
    {
        await AssertRpcError(() => _tasks.UpdateTaskItemAsync(Update(999, new TaskItem { Title = "x" }, "title")).ResponseAsync, StatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteTaskItem_ExistingTask_RemovesIt()
    {
        var created = await _tasks.CreateTaskItemAsync(new CreateTaskItemRequest { BoardId = await NewBoardId(), Title = "T" });

        await _tasks.DeleteTaskItemAsync(new DeleteTaskItemRequest { Id = created.Id });

        await AssertRpcError(() => _tasks.GetTaskItemAsync(new GetTaskItemRequest { Id = created.Id }).ResponseAsync, StatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteTaskItem_UnknownId_ReturnsNotFound()
    {
        await AssertRpcError(() => _tasks.DeleteTaskItemAsync(new DeleteTaskItemRequest { Id = 999 }).ResponseAsync, StatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteBoard_BoardWithTasks_CascadesToItsTasks()
    {
        var boardId = await NewBoardId();
        var task = await _tasks.CreateTaskItemAsync(new CreateTaskItemRequest { BoardId = boardId, Title = "T" });

        await _boards.DeleteBoardAsync(new DeleteBoardRequest { Id = boardId });

        await AssertRpcError(() => _tasks.GetTaskItemAsync(new GetTaskItemRequest { Id = task.Id }).ResponseAsync, StatusCode.NotFound);
    }

    [Fact]
    public async Task ListTaskItems_FilteredByBoard_ReturnsOnlyThatBoardsTasks()
    {
        var b1 = await NewBoardId();
        var b2 = await NewBoardId();
        await _tasks.CreateTaskItemAsync(new CreateTaskItemRequest { BoardId = b1, Title = "in1" });
        await _tasks.CreateTaskItemAsync(new CreateTaskItemRequest { BoardId = b2, Title = "in2" });

        var list = await _tasks.ListTaskItemsAsync(new ListTaskItemsRequest { BoardId = b1 });

        Assert.Equal(new[] { "in1" }, list.TaskItems.Select(t => t.Title));
        Assert.Equal(string.Empty, list.NextPageToken);
    }

    [Fact]
    public async Task ListTaskItems_Paginated_WalksAllPagesInIdOrder()
    {
        var boardId = await NewBoardId();
        for (var i = 1; i <= 5; i++)
            await _tasks.CreateTaskItemAsync(new CreateTaskItemRequest { BoardId = boardId, Title = $"t{i}" });

        var titles = new List<string>();
        var token = string.Empty;
        var pages = 0;
        do
        {
            var page = await _tasks.ListTaskItemsAsync(new ListTaskItemsRequest { BoardId = boardId, PageSize = 2, PageToken = token });
            titles.AddRange(page.TaskItems.Select(t => t.Title));
            token = page.NextPageToken;
            pages++;
        } while (token != string.Empty);

        Assert.Equal(3, pages);
        Assert.Equal(new[] { "t1", "t2", "t3", "t4", "t5" }, titles);
    }

    // A token this server never issued must fail loudly: silently restarting at page one would make
    // a client that echoes a bad token loop over the first page forever. "-1" is covered too -
    // SQLite itself would accept a negative OFFSET and shift the next token instead of failing.
    [Theory]
    [InlineData("abc")]
    [InlineData("-1")]
    [InlineData("+1")]
    [InlineData(" 1")]
    [InlineData("99999999999")]
    public async Task ListTaskItems_MalformedPageToken_ReturnsInvalidArgument(string token)
    {
        var boardId = await NewBoardId();

        await AssertRpcError(
            () => _tasks.ListTaskItemsAsync(new ListTaskItemsRequest { BoardId = boardId, PageSize = 2, PageToken = token }).ResponseAsync,
            StatusCode.InvalidArgument);
    }

    [Fact]
    public async Task ListTaskItems_ExactlyOnePageOfResults_HasNoNextPageToken()
    {
        var boardId = await NewBoardId();
        for (var i = 0; i < 2; i++)
            await _tasks.CreateTaskItemAsync(new CreateTaskItemRequest { BoardId = boardId, Title = "t" });

        var page = await _tasks.ListTaskItemsAsync(new ListTaskItemsRequest { BoardId = boardId, PageSize = 2 });

        Assert.Equal(2, page.TaskItems.Count);
        Assert.Equal(string.Empty, page.NextPageToken);
    }

    [Fact]
    public async Task ListTaskItems_HugePageSize_IsCappedInsteadOfFailing()
    {
        var boardId = await NewBoardId();
        for (var i = 0; i < 201; i++)
            await _tasks.CreateTaskItemAsync(new CreateTaskItemRequest { BoardId = boardId, Title = "t" });

        var page = await _tasks.ListTaskItemsAsync(new ListTaskItemsRequest { BoardId = boardId, PageSize = int.MaxValue });

        Assert.Equal(200, page.TaskItems.Count);
        Assert.Equal("200", page.NextPageToken);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateTaskItem_BlankTitle_ReturnsInvalidArgument(string title)
    {
        var boardId = await NewBoardId();

        await AssertRpcError(() => _tasks.CreateTaskItemAsync(new CreateTaskItemRequest { BoardId = boardId, Title = title }).ResponseAsync, StatusCode.InvalidArgument);
    }

    [Fact]
    public async Task UpdateTaskItem_UnknownMaskPath_ReturnsInvalidArgumentAndChangesNothing()
    {
        var created = await _tasks.CreateTaskItemAsync(new CreateTaskItemRequest { BoardId = await NewBoardId(), Title = "keep" });

        // The valid path comes first on purpose: it must not be applied when a later path is rejected.
        var request = Update(created.Id, new TaskItem { Title = "changed" }, "title", "no_such_field");

        await AssertRpcError(() => _tasks.UpdateTaskItemAsync(request).ResponseAsync, StatusCode.InvalidArgument);

        Assert.Equal("keep", (await _tasks.GetTaskItemAsync(new GetTaskItemRequest { Id = created.Id })).Title);
    }

    [Fact]
    public async Task UpdateTaskItem_MissingTaskItem_ReturnsInvalidArgument()
    {
        var created = await _tasks.CreateTaskItemAsync(new CreateTaskItemRequest { BoardId = await NewBoardId(), Title = "T" });
        var request = new UpdateTaskItemRequest { Id = created.Id, UpdateMask = new FieldMask { Paths = { "title" } } };

        await AssertRpcError(() => _tasks.UpdateTaskItemAsync(request).ResponseAsync, StatusCode.InvalidArgument);
    }

    [Fact]
    public async Task UpdateTaskItem_BlankTitleInMask_ReturnsInvalidArgumentAndChangesNothing()
    {
        var created = await _tasks.CreateTaskItemAsync(new CreateTaskItemRequest { BoardId = await NewBoardId(), Title = "keep" });

        // Two paths on purpose: the valid one must not be applied when the other is rejected.
        var request = Update(created.Id, new TaskItem { Title = " ", Description = "changed" }, "description", "title");

        await AssertRpcError(() => _tasks.UpdateTaskItemAsync(request).ResponseAsync, StatusCode.InvalidArgument);

        var stored = await _tasks.GetTaskItemAsync(new GetTaskItemRequest { Id = created.Id });
        Assert.Equal("keep", stored.Title);
        Assert.Equal(string.Empty, stored.Description);
    }
}
