using TaskBoard.Application.Mapping;
using TaskBoard.Contracts.V1;
using DomainBoard = TaskBoard.Domain.Board;

namespace TaskBoard.Api.Tests.Mapping;

public class BoardMappingExtensionsTests
{
    [Fact]
    public void ToProto_UnspecifiedKindCreatedAt_IsTreatedAsUtc()
    {
        // SQLite returns DateTime with Kind=Unspecified; without SpecifyKind, Timestamp.FromDateTime throws.
        var board = new DomainBoard { Id = 3, Name = "B", Description = "D", CreatedAt = new DateTime(2024, 5, 6, 7, 8, 9, DateTimeKind.Unspecified) };

        var proto = board.ToProto();

        Assert.Equal(3, proto.Id);
        Assert.Equal("B", proto.Name);
        Assert.Equal("D", proto.Description);
        Assert.Equal(new DateTime(2024, 5, 6, 7, 8, 9, DateTimeKind.Utc), proto.CreatedAt.ToDateTime());
    }

    [Fact]
    public void ToDomain_WithoutDescription_KeepsDefaultEmptyDescription()
    {
        var board = new CreateBoardRequest { Name = "B" }.ToDomain();

        Assert.Equal("B", board.Name);
        Assert.Equal(string.Empty, board.Description);
        Assert.Equal(DateTimeKind.Utc, board.CreatedAt.Kind);
    }

    [Fact]
    public void ToDomain_WithDescription_CopiesIt()
    {
        var board = new CreateBoardRequest { Name = "B", Description = "D" }.ToDomain();

        Assert.Equal("D", board.Description);
    }

    [Fact]
    public void ApplyTo_OnlyNameSet_LeavesDescriptionUntouched()
    {
        var entity = new DomainBoard { Name = "old", Description = "keep", CreatedAt = new DateTime(2020, 1, 1) };

        new UpdateBoardRequest { Id = 1, Name = "new" }.ApplyTo(entity);

        Assert.Equal("new", entity.Name);
        Assert.Equal("keep", entity.Description);
    }

    [Fact]
    public void ApplyTo_EmptyDescriptionExplicitlySet_ClearsDescription()
    {
        // The point of `optional`: an explicit empty string must be distinguishable from "not sent".
        var entity = new DomainBoard { Name = "n", Description = "something" };

        new UpdateBoardRequest { Id = 1, Description = "" }.ApplyTo(entity);

        Assert.Equal(string.Empty, entity.Description);
        Assert.Equal("n", entity.Name);
    }

    [Fact]
    public void ApplyTo_NothingSet_ChangesNothingAndKeepsCreatedAt()
    {
        var created = new DateTime(2020, 1, 1);
        var entity = new DomainBoard { Name = "n", Description = "d", CreatedAt = created };

        new UpdateBoardRequest { Id = 1 }.ApplyTo(entity);

        Assert.Equal("n", entity.Name);
        Assert.Equal("d", entity.Description);
        Assert.Equal(created, entity.CreatedAt);
    }
}
