using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using TaskBoard.Contracts.V1;

namespace TaskBoard.Api.Services;

// Named LabelGrpcService because the generated static class is already called LabelService.
public class LabelGrpcService : LabelService.LabelServiceBase
{
    private readonly ILogger<LabelGrpcService> _logger;

    public LabelGrpcService(ILogger<LabelGrpcService> logger)
    {
        _logger = logger;
    }

    public override Task<Label> CreateLabel(CreateLabelRequest request, ServerCallContext context)
    {
        throw new RpcException(new Status(StatusCode.Unimplemented, "Will be implemented with EF Core."));
    }

    public override Task<Label> GetLabel(GetLabelRequest request, ServerCallContext context)
    {
        throw new RpcException(new Status(StatusCode.Unimplemented, "Will be implemented with EF Core."));
    }

    public override Task<ListLabelsResponse> ListLabels(ListLabelsRequest request, ServerCallContext context)
    {
        throw new RpcException(new Status(StatusCode.Unimplemented, "Will be implemented with EF Core."));
    }

    public override Task<Label> UpdateLabel(UpdateLabelRequest request, ServerCallContext context)
    {
        throw new RpcException(new Status(StatusCode.Unimplemented, "Will be implemented with EF Core."));
    }

    public override Task<Empty> DeleteLabel(DeleteLabelRequest request, ServerCallContext context)
    {
        throw new RpcException(new Status(StatusCode.Unimplemented, "Will be implemented with EF Core."));
    }
}
