using Grpc.Core;
using Google.Protobuf.WellKnownTypes;
using Microsoft.EntityFrameworkCore;
using TaskBoard.Api.Streaming;
using TaskBoard.Api.Validation;
using TaskBoard.Application.Mapping;
using TaskBoard.Contracts.V1;
using TaskBoard.Infrastructure.Persistence;
using DomainLabel = TaskBoard.Domain.Label;

namespace TaskBoard.Api.Services;

public class TaskItemGrpcService : TaskItemService.TaskItemServiceBase
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 200;

    // Keep in sync with the switch in UpdateTaskItem.
    private static readonly HashSet<string> UpdatablePaths =
        ["title", "description", "status", "priority", "due_date", "label_ids"];

    private readonly ILogger<TaskItemGrpcService> _logger;
    private readonly TaskBoardDbContext _dbContext;
    private readonly IBoardEventBroadcaster _broadcaster;

    public TaskItemGrpcService(ILogger<TaskItemGrpcService> logger, TaskBoardDbContext dbContext, IBoardEventBroadcaster broadcaster)
    {
        _logger = logger;
        _dbContext = dbContext;
        _broadcaster = broadcaster;
    }

    public override async Task<TaskItem> CreateTaskItem(CreateTaskItemRequest request, ServerCallContext context)
    {
        RequestValidation.RequireTitle(request.Title);

        var boardExists = await _dbContext.Boards.AnyAsync(b => b.Id == request.BoardId, context.CancellationToken);
        if (!boardExists)
            throw new RpcException(new Status(StatusCode.NotFound, $"Board {request.BoardId} not found."));

        var entity = request.ToDomain();

        if (request.LabelIds.Count > 0)
            entity.Labels = await ResolveLabelsAsync(request.LabelIds, context.CancellationToken);

        _dbContext.Tasks.Add(entity);
        await _dbContext.SaveChangesAsync(context.CancellationToken);

        // Published only after SaveChanges succeeded: a watcher must never see a task that
        // failed to persist.
        var created = entity.ToProto();
        _broadcaster.Publish(entity.BoardId, new BoardEvent { TaskCreated = created });
        return created;
    }

    public override async Task<TaskItem> GetTaskItem(GetTaskItemRequest request, ServerCallContext context)
    {
        var entity = await _dbContext.Tasks
            .Include(t => t.Labels)
            .FirstOrDefaultAsync(t => t.Id == request.Id, context.CancellationToken);

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
        var offset = RequestValidation.ParsePageToken(request.PageToken);

        // Capped: the client chooses page_size, so without a ceiling one call could ask for every row
        // (and pageSize + 1 below would overflow for int.MaxValue).
        var pageSize = request.PageSize > 0 ? Math.Min(request.PageSize, MaxPageSize) : DefaultPageSize;

        // Fetch one extra row to know whether a next page exists, without a second COUNT query.
        var page = await query.Skip(offset).Take(pageSize + 1).ToListAsync(context.CancellationToken);
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

        if (request.TaskItem is null)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "task_item must carry the new values."));

        var entity = await _dbContext.Tasks
            .Include(t => t.Labels)
            .FirstOrDefaultAsync(t => t.Id == request.Id, context.CancellationToken);

        if (entity is null)
            throw new RpcException(new Status(StatusCode.NotFound, $"Task {request.Id} not found."));

        // Everything that can reject the request runs BEFORE the entity is touched, so a failure
        // never leaves a half-applied change on the tracked entity.
        // The transcoder passes a REST query like ?updateMask=labelIds through as written, but the
        // paths below are the proto (snake_case) names. JSON shows camelCase names, so a REST caller
        // naturally sends those; normalising accepts both and leaves gRPC callers (snake_case) unchanged.
        var paths = request.UpdateMask.Paths.Select(RequestValidation.ToSnakeCase).ToList();

        var unknownPath = paths.FirstOrDefault(path => !UpdatablePaths.Contains(path));
        if (unknownPath is not null)
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"Unknown update_mask path '{unknownPath}'."));

        if (paths.Contains("title"))
            RequestValidation.RequireTitle(request.TaskItem.Title);

        var labels = paths.Contains("label_ids")
            ? await ResolveLabelsAsync(request.TaskItem.LabelIds, context.CancellationToken)
            : null;

        // FieldMask-driven update: only the paths the client listed get applied - everything
        // else in request.TaskItem is ignored, even if it carries a value.
        foreach (var path in paths)
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
                    entity.Labels = labels!; // resolved above because the mask contains this path
                    break;
                default:
                    // Unreachable while UpdatablePaths and this switch list the same paths.
                    throw new InvalidOperationException($"Update mask path '{path}' is allowed but not handled.");
            }
        }

        entity.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(context.CancellationToken);

        var updated = entity.ToProto();
        _broadcaster.Publish(entity.BoardId, new BoardEvent { TaskUpdated = updated });
        return updated;
    }

    public override async Task<Empty> DeleteTaskItem(DeleteTaskItemRequest request, ServerCallContext context)
    {
        var entity = await _dbContext.Tasks.FindAsync([request.Id], context.CancellationToken);
        if (entity is null)
            throw new RpcException(new Status(StatusCode.NotFound, $"Task {request.Id} not found."));

        _dbContext.Tasks.Remove(entity);
        await _dbContext.SaveChangesAsync(context.CancellationToken);

        _broadcaster.Publish(entity.BoardId, new BoardEvent { TaskDeletedId = entity.Id });
        return new Empty();
    }

    // NotFound, not InvalidArgument, to match the board_id check in CreateTaskItem: both are ids of
    // other resources inside the request body, and one rule is easier for clients than two.
    // Rejecting instead of silently dropping matters because a stale id (label deleted in another
    // tab) would otherwise "succeed" and the client would never learn the label was lost.
    private async Task<List<DomainLabel>> ResolveLabelsAsync(IEnumerable<int> requestedIds, CancellationToken cancellationToken)
    {
        var ids = requestedIds.Distinct().ToList();
        var labels = await _dbContext.Labels.Where(l => ids.Contains(l.Id)).ToListAsync(cancellationToken);

        var missing = ids.Except(labels.Select(l => l.Id)).ToList();
        if (missing.Count > 0)
            throw new RpcException(new Status(StatusCode.NotFound, $"Label(s) {string.Join(", ", missing)} not found."));

        return labels;
    }
}
