using TaskBoard.Contracts.V1;
using DomainLabel = TaskBoard.Domain.Label;

namespace TaskBoard.Application.Mapping;

public static class LabelMappingExtensions
{
    public static Label ToProto(this DomainLabel label) => new()
    {
        Id = label.Id,
        Name = label.Name,
        Color = label.Color
    };

    public static DomainLabel ToDomain(this CreateLabelRequest request) => new()
    {
        Name = request.Name,
        Color = request.Color
    };

    // Applies the request's values onto an existing tracked entity, in place -
    // there is nothing to return, EF Core picks up the change via change tracking.
    public static void ApplyTo(this UpdateLabelRequest request, DomainLabel entity)
    {
        entity.Name = request.Name;
        entity.Color = request.Color;
    }
}
