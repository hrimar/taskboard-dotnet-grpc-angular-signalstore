using Grpc.Core;
using TaskBoard.Contracts.V1;

namespace TaskBoard.Api.Tests.Services;

public class WatchBoardStreamingTests : GrpcTestBase
{
    private readonly BoardService.BoardServiceClient _boards;
    private readonly TaskItemService.TaskItemServiceClient _tasks;

    public WatchBoardStreamingTests()
    {
        _boards = new BoardService.BoardServiceClient(Channel);
        _tasks = new TaskItemService.TaskItemServiceClient(Channel);
    }

    // A watcher that never gets its event must fail the test, not hang it.
    private static CancellationToken Timeout() => new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token;

    private async Task<int> NewBoardId() =>
        (await _boards.CreateBoardAsync(new CreateBoardRequest { Name = "b" })).Id;

    // Subscription is registered when the server handler starts, which happens asynchronously
    // after the call is made; the NotFound check and Subscribe() run before the first event can
    // be read, so reading the first header is the point after which publishing is safe.
    private async Task<AsyncServerStreamingCall<BoardEvent>> Watch(int boardId, CancellationToken ct)
    {
        var call = _boards.WatchBoard(new WatchBoardRequest { BoardId = boardId }, cancellationToken: ct);
        await call.ResponseHeadersAsync;
        return call;
    }

    [Fact]
    public async Task WatchBoard_TaskCreatedUpdatedDeleted_StreamsEachEventInOrder()
    {
        var ct = Timeout();
        var boardId = await NewBoardId();
        using var watch = await Watch(boardId, ct);

        var created = await _tasks.CreateTaskItemAsync(new CreateTaskItemRequest { BoardId = boardId, Title = "T" });
        await _tasks.UpdateTaskItemAsync(new UpdateTaskItemRequest
        {
            Id = created.Id,
            TaskItem = new TaskItem { Status = TaskItemStatus.Done },
            UpdateMask = new Google.Protobuf.WellKnownTypes.FieldMask { Paths = { "status" } }
        });
        await _tasks.DeleteTaskItemAsync(new DeleteTaskItemRequest { Id = created.Id });

        var events = new List<BoardEvent>();
        for (var i = 0; i < 3; i++)
        {
            Assert.True(await watch.ResponseStream.MoveNext(ct));
            events.Add(watch.ResponseStream.Current);
        }

        Assert.Equal(BoardEvent.EventOneofCase.TaskCreated, events[0].EventCase);
        Assert.Equal(created.Id, events[0].TaskCreated.Id);
        Assert.Equal(BoardEvent.EventOneofCase.TaskUpdated, events[1].EventCase);
        Assert.Equal(TaskItemStatus.Done, events[1].TaskUpdated.Status);
        Assert.Equal(BoardEvent.EventOneofCase.TaskDeletedId, events[2].EventCase);
        Assert.Equal(created.Id, events[2].TaskDeletedId);
    }

    [Fact]
    public async Task WatchBoard_TaskOnAnotherBoard_IsNotDelivered()
    {
        var ct = Timeout();
        var watched = await NewBoardId();
        var other = await NewBoardId();
        using var watch = await Watch(watched, ct);

        await _tasks.CreateTaskItemAsync(new CreateTaskItemRequest { BoardId = other, Title = "elsewhere" });
        var mine = await _tasks.CreateTaskItemAsync(new CreateTaskItemRequest { BoardId = watched, Title = "mine" });

        // If the other board's event leaked, it would arrive first and fail the id check.
        Assert.True(await watch.ResponseStream.MoveNext(ct));
        Assert.Equal(mine.Id, watch.ResponseStream.Current.TaskCreated.Id);
    }

    [Fact]
    public async Task WatchBoard_UnknownBoard_ReturnsNotFound()
    {
        var ct = Timeout();
        using var call = _boards.WatchBoard(new WatchBoardRequest { BoardId = 999 }, cancellationToken: ct);

        await AssertRpcError(async () => await call.ResponseStream.MoveNext(ct), StatusCode.NotFound);
    }
}
