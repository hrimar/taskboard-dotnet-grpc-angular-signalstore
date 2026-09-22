using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using TaskBoard.Application.Mapping;
using TaskBoard.Contracts.V1;
using TaskBoard.Infrastructure.Persistence;

namespace TaskBoard.Api.Services;

// Named LabelGrpcService because the generated static class is already called LabelService.
public class LabelGrpcService : LabelService.LabelServiceBase
{
    private readonly ILogger<LabelGrpcService> _logger;
    private readonly TaskBoardDbContext _dbContext;

    public LabelGrpcService(ILogger<LabelGrpcService> logger, TaskBoardDbContext dbContext)
    {
        _logger = logger;
        _dbContext = dbContext;
    }

    public override async Task<Label> CreateLabel(CreateLabelRequest request, ServerCallContext context)
    {
        var entity = request.ToDomain();
        _dbContext.Labels.Add(entity);
        await _dbContext.SaveChangesAsync();
        return entity.ToProto();
    }

    public override async Task<Label> GetLabel(GetLabelRequest request, ServerCallContext context)
    {
        var entity = await _dbContext.Labels.FindAsync(request.Id);
        if (entity is null)
            throw new RpcException(new Status(StatusCode.NotFound, $"Label {request.Id} not found."));

        return entity.ToProto();
    }

    public override async Task<ListLabelsResponse> ListLabels(ListLabelsRequest request, ServerCallContext context)
    {
        var response = new ListLabelsResponse();
        var labels = await _dbContext.Labels.AsNoTracking().ToListAsync();
        response.Labels.AddRange(labels.Select(l => l.ToProto()));
        return response;
    }

    public override async Task<Label> UpdateLabel(UpdateLabelRequest request, ServerCallContext context)
    {
        var entity = await _dbContext.Labels.FindAsync(request.Id);
        if (entity is null)
            throw new RpcException(new Status(StatusCode.NotFound, $"Label {request.Id} not found."));

        request.ApplyTo(entity);
        await _dbContext.SaveChangesAsync();
        return entity.ToProto();
    }

    public override async Task<Empty> DeleteLabel(DeleteLabelRequest request, ServerCallContext context)
    {
        var entity = await _dbContext.Labels.FindAsync(request.Id);
        if (entity is null)
            throw new RpcException(new Status(StatusCode.NotFound, $"Label {request.Id} not found."));

        _dbContext.Labels.Remove(entity);
        await _dbContext.SaveChangesAsync();
        return new Empty();
    }
}
