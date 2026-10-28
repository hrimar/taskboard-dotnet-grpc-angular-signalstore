using TaskBoard.Application.Mapping;
using TaskBoard.Contracts.V1;
using DomainLabel = TaskBoard.Domain.Label;

namespace TaskBoard.Api.Tests.Mapping;

public class LabelMappingExtensionsTests
{
    [Fact]
    public void ToProto_CopiesAllFields()
    {
        var proto = new DomainLabel { Id = 4, Name = "Bug", Color = "#FF0000" }.ToProto();

        Assert.Equal(4, proto.Id);
        Assert.Equal("Bug", proto.Name);
        Assert.Equal("#FF0000", proto.Color);
    }

    [Fact]
    public void ToDomain_CopiesNameAndColorButNotId()
    {
        var label = new CreateLabelRequest { Name = "Bug", Color = "#FF0000" }.ToDomain();

        Assert.Equal(0, label.Id);
        Assert.Equal("Bug", label.Name);
        Assert.Equal("#FF0000", label.Color);
    }

    [Fact]
    public void ApplyTo_OverwritesNameAndColorInPlace()
    {
        var entity = new DomainLabel { Id = 9, Name = "a", Color = "#000000" };

        new UpdateLabelRequest { Id = 9, Name = "b", Color = "#FFFFFF" }.ApplyTo(entity);

        Assert.Equal(9, entity.Id);
        Assert.Equal("b", entity.Name);
        Assert.Equal("#FFFFFF", entity.Color);
    }
}
