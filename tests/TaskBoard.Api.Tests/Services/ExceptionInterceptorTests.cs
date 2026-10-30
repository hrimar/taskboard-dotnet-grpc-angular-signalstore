using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TaskBoard.Api.Streaming;
using TaskBoard.Contracts.V1;

namespace TaskBoard.Api.Tests.Services;

// The broadcaster is the one collaborator both a unary call (Publish after save) and the stream
// (Subscribe) go through, so replacing it with a throwing one triggers an unexpected exception
// in each handler kind without touching the services.
public class ExceptionInterceptorTests : IDisposable
{
    private const string SecretDetail = "secret-internal-detail";

    private readonly TaskBoardApiFactory _factory = new();
    private readonly GrpcChannel _channel;

    public ExceptionInterceptorTests()
    {
        var failing = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.Replace(ServiceDescriptor.Singleton<IBoardEventBroadcaster, ThrowingBroadcaster>())));

        _channel = GrpcChannel.ForAddress(failing.Server.BaseAddress, new GrpcChannelOptions { HttpClient = failing.CreateClient() });
    }

    [Fact]
    public async Task UnaryCall_UnexpectedException_BecomesGenericInternal()
    {
        var boards = new BoardService.BoardServiceClient(_channel);
        var tasks = new TaskItemService.TaskItemServiceClient(_channel);
        var board = await boards.CreateBoardAsync(new CreateBoardRequest { Name = "b" });

        // The task is saved, then Publish throws. The real broadcaster cannot fail this way - this
        // only borrows it as a convenient place to make a handler throw something unexpected.
        var ex = await Assert.ThrowsAsync<RpcException>(async () =>
            await tasks.CreateTaskItemAsync(new CreateTaskItemRequest { BoardId = board.Id, Title = "T" }));

        Assert.Equal(StatusCode.Internal, ex.StatusCode);
        Assert.DoesNotContain(SecretDetail, ex.Status.Detail);
    }

    [Fact]
    public async Task StreamingCall_UnexpectedException_BecomesGenericInternal()
    {
        var boards = new BoardService.BoardServiceClient(_channel);
        var board = await boards.CreateBoardAsync(new CreateBoardRequest { Name = "b" });

        using var call = boards.WatchBoard(new WatchBoardRequest { BoardId = board.Id });
        var ex = await Assert.ThrowsAsync<RpcException>(async () => await call.ResponseStream.MoveNext(CancellationToken.None));

        Assert.Equal(StatusCode.Internal, ex.StatusCode);
        Assert.DoesNotContain(SecretDetail, ex.Status.Detail);
    }

    [Fact]
    public async Task ExpectedRpcException_PassesThroughUnchanged()
    {
        var boards = new BoardService.BoardServiceClient(_channel);

        var ex = await Assert.ThrowsAsync<RpcException>(async () => await boards.GetBoardAsync(new GetBoardRequest { Id = 999 }));

        Assert.Equal(StatusCode.NotFound, ex.StatusCode);
        Assert.Equal("Board 999 not found.", ex.Status.Detail);
    }

    public void Dispose()
    {
        _channel.Dispose();
        _factory.Dispose();
    }

    private sealed class ThrowingBroadcaster : IBoardEventBroadcaster
    {
        public void Publish(int boardId, BoardEvent boardEvent) => throw new InvalidOperationException(SecretDetail);

        public BoardSubscription Subscribe(int boardId) => throw new InvalidOperationException(SecretDetail);
    }
}
