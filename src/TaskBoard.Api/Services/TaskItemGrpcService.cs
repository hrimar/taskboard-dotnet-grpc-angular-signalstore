using Grpc.Core;
using Google.Protobuf.WellKnownTypes;
using Microsoft.EntityFrameworkCore;
using TaskBoard.Application.Mapping;
using TaskBoard.Contracts.V1;
using TaskBoard.Infrastructure.Persistence;

namespace TaskBoard.Api.Services;

public class TaskItemGrpcService : TaskItemService.TaskItemServiceBase
{
    private const int DefaultPageSize = 20;

    private readonly ILogger<TaskItemGrpcService> _logger;
    private readonly TaskBoardDbContext _dbContext;

    public TaskItemGrpcService(ILogger<TaskItemGrpcService> logger, TaskBoardDbContext dbContext)
    {
        _logger = logger;
        _dbContext = dbContext;
    }

    public override async Task<TaskItem> CreateTaskItem(CreateTaskItemRequest request, ServerCallContext context)
    {
        var boardExists = await _dbContext.Boards.AnyAsync(b => b.Id == request.BoardId);
        if (!boardExists)
            throw new RpcException(new Status(StatusCode.NotFound, $"Board {request.BoardId} not found."));

        var entity = request.ToDomain();

        if (request.LabelIds.Count > 0)
        {
            entity.Labels = await _dbContext.Labels
                .Where(l => request.LabelIds.Contains(l.Id))
                .ToListAsync();
        }

        _dbContext.Tasks.Add(entity);
        await _dbContext.SaveChangesAsync();
        return entity.ToProto();
    }

    public override async Task<TaskItem> GetTaskItem(GetTaskItemRequest request, ServerCallContext context)
    {
        var entity = await _dbContext.Tasks
            .Include(t => t.Labels)
            .FirstOrDefaultAsync(t => t.Id == request.Id);

        if (entity is null)
            throw new RpcException(new Status(StatusCode.NotFound, $"Task {request.Id} not found."));

        return entity.ToProto();
    }

    public override async Task<ListTaskItemsResponse> ListTaskItems(ListTaskItemsRequest request, ServerCallContext context)
    {
        var query = _dbContext.Tasks.Include(t => t.Labels).AsNoTracking().AsQueryable();

        if (request.BoardId != 0)
            query = query.Where(t => t.BoardId == request.BoardId);

        query = query.OrderBy(t => t.Id);

        // Simplified offset pagination: page_token is just the row offset, encoded as a
        // string so it stays an opaque value from the client's point of view. A production
        // API would use a cursor over a stable key instead, so results stay correct if rows
        // are inserted/deleted between pages - out of scope for this learning project.
        var offset = 0;
        if (!string.IsNullOrEmpty(request.PageToken) && int.TryParse(request.PageToken, out var parsedOffset))
            offset = parsedOffset;

        var pageSize = request.PageSize > 0 ? request.PageSize : DefaultPageSize;

        // Fetch one extra row to know whether a next page exists, without a second COUNT query.
        var page = await query.Skip(offset).Take(pageSize + 1).ToListAsync();
        var hasNextPage = page.Count > pageSize;
        var items = hasNextPage ? page.Take(pageSize) : page;

        var response = new ListTaskItemsResponse
        {
            NextPageToken = hasNextPage ? (offset + pageSize).ToString() : string.Empty
        };
        response.TaskItems.AddRange(items.Select(t => t.ToProto()));
        return response;
    }

    public override async Task<TaskItem> UpdateTaskItem(UpdateTaskItemRequest request, ServerCallContext context)
    {
        if (request.UpdateMask is null || request.UpdateMask.Paths.Count == 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "update_mask must list at least one field."));

        var entity = await _dbContext.Tasks
            .Include(t => t.Labels)
            .FirstOrDefaultAsync(t => t.Id == request.Id);

        if (entity is null)
            throw new RpcException(new Status(StatusCode.NotFound, $"Task {request.Id} not found."));

        // FieldMask-driven update: only the paths the client listed get applied - everything
        // else in request.TaskItem is ignored, even if it carries a value.
        foreach (var path in request.UpdateMask.Paths)
        {
            switch (path)
            {
                case "title":
                    entity.Title = request.TaskItem.Title;
                    break;
                case "description":
                    entity.Description = request.TaskItem.Description;
                    break;
                case "status":
                    entity.Status = request.TaskItem.Status.ToDomain();
                    break;
                case "priority":
                    entity.Priority = request.TaskItem.Priority.ToDomain();
                    break;
                case "due_date":
                    entity.DueDate = request.TaskItem.DueDate?.ToDateTime();
                    break;
                case "label_ids":
                    entity.Labels = await _dbContext.Labels
                        .Where(l => request.TaskItem.LabelIds.Contains(l.Id))
                        .ToListAsync();
                    break;
                default:
                    throw new RpcException(new Status(StatusCode.InvalidArgument, $"Unknown update_mask path '{path}'."));
            }
        }

        entity.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();
        return entity.ToProto();
    }

    public override async Task<Empty> DeleteTaskItem(DeleteTaskItemRequest request, ServerCallContext context)
    {
        var entity = await _dbContext.Tasks.FindAsync(request.Id);
        if (entity is null)
            throw new RpcException(new Status(StatusCode.NotFound, $"Task {request.Id} not found."));

        _dbContext.Tasks.Remove(entity);
        await _dbContext.SaveChangesAsync();
        return new Empty();
    }
}
