using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using TaskBoard.Application.Mapping;
using TaskBoard.Contracts.V1;
using TaskBoard.Infrastructure.Persistence;

namespace TaskBoard.Api.Services;

public class BoardGrpcService : BoardService.BoardServiceBase
{
    private readonly ILogger<BoardGrpcService> _logger;
    private readonly TaskBoardDbContext _dbContext;

    public BoardGrpcService(ILogger<BoardGrpcService> logger, TaskBoardDbContext dbContext)
    {
        _logger = logger;
        _dbContext = dbContext;
    }

    public override async Task<Board> CreateBoard(CreateBoardRequest request, ServerCallContext context)
    {
        var entity = request.ToDomain();
        _dbContext.Boards.Add(entity);
        await _dbContext.SaveChangesAsync();
        return entity.ToProto();
    }

    public override async Task<Board> GetBoard(GetBoardRequest request, ServerCallContext context)
    {
        var entity = await _dbContext.Boards.FindAsync(request.Id);
        if (entity is null)
            throw new RpcException(new Status(StatusCode.NotFound, $"Board {request.Id} not found."));

        return entity.ToProto();
    }

    public override async Task<ListBoardsResponse> ListBoards(ListBoardsRequest request, ServerCallContext context)
    {
        var response = new ListBoardsResponse();
        var boards = await _dbContext.Boards.AsNoTracking().ToListAsync();
        response.Boards.AddRange(boards.Select(b => b.ToProto()));
        return response;
    }

    public override async Task<Board> UpdateBoard(UpdateBoardRequest request, ServerCallContext context)
    {
        var entity = await _dbContext.Boards.FindAsync(request.Id);
        if (entity is null)
            throw new RpcException(new Status(StatusCode.NotFound, $"Board {request.Id} not found."));

        request.ApplyTo(entity);
        await _dbContext.SaveChangesAsync();
        return entity.ToProto();
    }

    public override async Task<Empty> DeleteBoard(DeleteBoardRequest request, ServerCallContext context)
    {
        var entity = await _dbContext.Boards.FindAsync(request.Id);
        if (entity is null)
            throw new RpcException(new Status(StatusCode.NotFound, $"Board {request.Id} not found."));

        _dbContext.Boards.Remove(entity);
        await _dbContext.SaveChangesAsync();
        return new Empty();
    }
}
