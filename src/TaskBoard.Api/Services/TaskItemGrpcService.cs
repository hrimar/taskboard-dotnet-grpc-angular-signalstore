using Grpc.Core;
using Google.Protobuf.WellKnownTypes;
using TaskBoard.Contracts.V1;

namespace TaskBoard.Api.Services;

public class TaskItemGrpcService : TaskItemService.TaskItemServiceBase
{
    private readonly ILogger<TaskItemGrpcService> _logger;
    public TaskItemGrpcService(ILogger<TaskItemGrpcService> logger)
    {
        _logger = logger;
    }
    public override Task<TaskItem> CreateTaskItem(CreateTaskItemRequest request, ServerCallContext context)
    {
        throw new RpcException(new Status(StatusCode.Unimplemented, "Will be implemented with EF Core."));
    }
    public override Task<TaskItem> GetTaskItem(GetTaskItemRequest request, ServerCallContext context)
    {
        throw new RpcException(new Status(StatusCode.Unimplemented, "Will be implemented with EF Core."));
    }
    public override Task<ListTaskItemsResponse> ListTaskItems(ListTaskItemsRequest request, ServerCallContext context)
    {
        throw new RpcException(new Status(StatusCode.Unimplemented, "Will be implemented with EF Core."));
    }
    public override Task<TaskItem> UpdateTaskItem(UpdateTaskItemRequest request, ServerCallContext context)
    {
        throw new RpcException(new Status(StatusCode.Unimplemented, "Will be implemented with EF Core."));
    }
    public override Task<Empty> DeleteTaskItem(DeleteTaskItemRequest request, ServerCallContext context)
    {
        throw new RpcException(new Status(StatusCode.Unimplemented, "Will be implemented with EF Core."));
    }
}
