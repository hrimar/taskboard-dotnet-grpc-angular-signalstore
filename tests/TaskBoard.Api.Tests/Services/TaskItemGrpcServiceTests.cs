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

    // The next two tests pin a known gap on purpose: label ids are resolved with a plain
    // Where(...Contains), so a stale id is dropped instead of rejected. If the service starts
    // answering NotFound/InvalidArgument (planned with the validation work), these must change.
    [Fact]
    public async Task CreateTaskItem_UnknownLabelId_IsSilentlyDropped()
    {
        var created = await _tasks.CreateTaskItemAsync(new CreateTaskItemRequest
        {
            BoardId = await NewBoardId(), Title = "T", LabelIds = { 999 }
        });

        Assert.Empty(created.LabelIds);
    }

    [Fact]
    public async Task UpdateTaskItem_UnknownLabelIdInMask_IsSilentlyDropped()
    {
        var known = await NewLabelId();
        var created = await _tasks.CreateTaskItemAsync(new CreateTaskItemRequest
        {
            BoardId = await NewBoardId(), Title = "T", LabelIds = { known }
        });

        var updated = await _tasks.UpdateTaskItemAsync(Update(created.Id, new TaskItem { LabelIds = { 999 } }, "label_ids"));

        Assert.Empty(updated.LabelIds);
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

    // Pins the current lenient handling of malformed tokens (see the page_token comment in the
    // service): a non-numeric token restarts at the first page. A client that echoes a bad
    // token therefore loops over page one - the reason this should become InvalidArgument.
    [Fact]
    public async Task ListTaskItems_NonNumericPageToken_RestartsAtFirstPage()
    {
        var boardId = await NewBoardId();
        for (var i = 1; i <= 3; i++)
            await _tasks.CreateTaskItemAsync(new CreateTaskItemRequest { BoardId = boardId, Title = $"t{i}" });

        var page = await _tasks.ListTaskItemsAsync(new ListTaskItemsRequest { BoardId = boardId, PageSize = 2, PageToken = "abc" });

        Assert.Equal(new[] { "t1", "t2" }, page.TaskItems.Select(t => t.Title));
        Assert.Equal("2", page.NextPageToken);
    }

    // SQLite treats a negative OFFSET as 0, so no error surfaces - but the next token is then
    // computed from the negative offset ("-1" + 2 = "1"), which silently shifts the paging window.
    [Fact]
    public async Task ListTaskItems_NegativePageToken_ReturnsFirstPageWithShiftedNextToken()
    {
        var boardId = await NewBoardId();
        for (var i = 1; i <= 3; i++)
            await _tasks.CreateTaskItemAsync(new CreateTaskItemRequest { BoardId = boardId, Title = $"t{i}" });

        var page = await _tasks.ListTaskItemsAsync(new ListTaskItemsRequest { BoardId = boardId, PageSize = 2, PageToken = "-1" });

        Assert.Equal(new[] { "t1", "t2" }, page.TaskItems.Select(t => t.Title));
        Assert.Equal("1", page.NextPageToken);
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
}
