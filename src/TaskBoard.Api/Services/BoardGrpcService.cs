using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using TaskBoard.Contracts.V1;

namespace TaskBoard.Api.Services;

public class BoardGrpcService : BoardService.BoardServiceBase
{
    private readonly ILogger<BoardGrpcService> _logger;

    public BoardGrpcService(ILogger<BoardGrpcService> logger)
    {
        _logger = logger;
    }

    public override Task<Board> CreateBoard(CreateBoardRequest request, ServerCallContext context)
    {
        throw new RpcException(new Status(StatusCode.Unimplemented, "Will be implemented with EF Core."));
    }

    public override Task<Board> GetBoard(GetBoardRequest request, ServerCallContext context)
    {
        throw new RpcException(new Status(StatusCode.Unimplemented, "Will be implemented with EF Core."));
    }

    public override Task<ListBoardsResponse> ListBoards(ListBoardsRequest request, ServerCallContext context)
    {
        throw new RpcException(new Status(StatusCode.Unimplemented, "Will be implemented with EF Core."));
    }

    public override Task<Board> UpdateBoard(UpdateBoardRequest request, ServerCallContext context)
    {
        throw new RpcException(new Status(StatusCode.Unimplemented, "Will be implemented with EF Core."));
    }

    public override Task<Empty> DeleteBoard(DeleteBoardRequest request, ServerCallContext context)
    {
        throw new RpcException(new Status(StatusCode.Unimplemented, "Will be implemented with EF Core."));
    }
}
