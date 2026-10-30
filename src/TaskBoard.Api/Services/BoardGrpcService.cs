using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using TaskBoard.Api.Streaming;
using TaskBoard.Api.Validation;
using TaskBoard.Application.Mapping;
using TaskBoard.Contracts.V1;
using TaskBoard.Infrastructure.Persistence;

namespace TaskBoard.Api.Services;

public class BoardGrpcService : BoardService.BoardServiceBase
{
    private readonly ILogger<BoardGrpcService> _logger;
    private readonly TaskBoardDbContext _dbContext;
    private readonly IBoardEventBroadcaster _broadcaster;

    public BoardGrpcService(ILogger<BoardGrpcService> logger, TaskBoardDbContext dbContext, IBoardEventBroadcaster broadcaster)
    {
        _logger = logger;
        _dbContext = dbContext;
        _broadcaster = broadcaster;
    }

    public override async Task<Board> CreateBoard(CreateBoardRequest request, ServerCallContext context)
    {
        RequestValidation.RequireName(request.Name);

        var entity = request.ToDomain();
        _dbContext.Boards.Add(entity);
        await _dbContext.SaveChangesAsync(context.CancellationToken);
        return entity.ToProto();
    }

    public override async Task<Board> GetBoard(GetBoardRequest request, ServerCallContext context)
    {
        var entity = await _dbContext.Boards.FindAsync([request.Id], context.CancellationToken);
        if (entity is null)
            throw new RpcException(new Status(StatusCode.NotFound, $"Board {request.Id} not found."));

        return entity.ToProto();
    }

    public override async Task<ListBoardsResponse> ListBoards(ListBoardsRequest request, ServerCallContext context)
    {
        var response = new ListBoardsResponse();
        var boards = await _dbContext.Boards.AsNoTracking().ToListAsync(context.CancellationToken);
        response.Boards.AddRange(boards.Select(b => b.ToProto()));
        return response;
    }

    public override async Task<Board> UpdateBoard(UpdateBoardRequest request, ServerCallContext context)
    {
        // Optional field: only a name that is actually sent must be valid.
        if (request.HasName)
            RequestValidation.RequireName(request.Name);

        var entity = await _dbContext.Boards.FindAsync([request.Id], context.CancellationToken);
        if (entity is null)
            throw new RpcException(new Status(StatusCode.NotFound, $"Board {request.Id} not found."));

        request.ApplyTo(entity);
        await _dbContext.SaveChangesAsync(context.CancellationToken);
        return entity.ToProto();
    }

    public override async Task<Empty> DeleteBoard(DeleteBoardRequest request, ServerCallContext context)
    {
        var entity = await _dbContext.Boards.FindAsync([request.Id], context.CancellationToken);
        if (entity is null)
            throw new RpcException(new Status(StatusCode.NotFound, $"Board {request.Id} not found."));

        _dbContext.Boards.Remove(entity);
        await _dbContext.SaveChangesAsync(context.CancellationToken);
        return new Empty();
    }

    public override async Task WatchBoard(WatchBoardRequest request, IServerStreamWriter<BoardEvent> responseStream, ServerCallContext context)
    {
        // Fail fast like the unary calls do; otherwise a typo'd id would open a stream that
        // silently never receives anything.
        if (!await _dbContext.Boards.AnyAsync(b => b.Id == request.BoardId, context.CancellationToken))
            throw new RpcException(new Status(StatusCode.NotFound, $"Board {request.BoardId} not found."));

        // `using` matters: it runs when the client disconnects (the cancellation below) and
        // removes the subscription, so closed streams don't pile up in the broadcaster.
        using var subscription = _broadcaster.Subscribe(request.BoardId);

        try
        {
            // gRPC sends response headers together with the first message. On a quiet board that
            // would be never, so the client could not tell "connected, nothing happened" from
            // "still connecting" (in the browser fetch() does not even resolve without headers).
            // Flushing them now makes the stream count as open as soon as it is subscribed.
            await context.WriteResponseHeadersAsync(new Metadata());

            // The stream stays open until the client goes away; context.CancellationToken fires
            // then (tab closed, navigation, network drop) and ends the loop.
            await foreach (var boardEvent in subscription.Reader.ReadAllAsync(context.CancellationToken))
                await responseStream.WriteAsync(boardEvent, context.CancellationToken);
        }
        catch (OperationCanceledException)
        {
            // The normal way a watch ends - not an error worth surfacing or logging.
        }
    }
}
