using Google.Protobuf.WellKnownTypes;
using TaskBoard.Contracts.V1;
using DomainBoard = TaskBoard.Domain.Board;

namespace TaskBoard.Application.Mapping;

public static class BoardMappingExtensions
{
    public static Board ToProto(this DomainBoard board) => new()
    {
        Id = board.Id,
        Name = board.Name,
        Description = board.Description,
        CreatedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(board.CreatedAt, DateTimeKind.Utc))
    };

    public static DomainBoard ToDomain(this CreateBoardRequest request)
    {
        var board = new DomainBoard
        {
            Name = request.Name,
            CreatedAt = DateTime.UtcNow
        };

        if (request.HasDescription)
            board.Description = request.Description;

        return board;
    }

    // Applies the request's values onto an existing tracked entity, in place.
    // CreatedAt is never touched here - it is set once, at creation.
    public static void ApplyTo(this UpdateBoardRequest request, DomainBoard entity)
    {
        if (request.HasName)
            entity.Name = request.Name;

        if (request.HasDescription)
            entity.Description = request.Description;
    }
}
