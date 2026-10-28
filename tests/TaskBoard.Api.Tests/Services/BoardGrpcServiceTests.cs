using Grpc.Core;
using TaskBoard.Contracts.V1;

namespace TaskBoard.Api.Tests.Services;

public class BoardGrpcServiceTests : GrpcTestBase
{
    private readonly BoardService.BoardServiceClient _client;

    public BoardGrpcServiceTests() => _client = new BoardService.BoardServiceClient(Channel);

    [Fact]
    public async Task CreateBoard_ValidRequest_ReturnsPersistedBoardWithIdAndUtcTimestamp()
    {
        var created = await _client.CreateBoardAsync(new CreateBoardRequest { Name = "Sprint", Description = "d" });

        Assert.True(created.Id > 0);
        Assert.Equal("Sprint", created.Name);
        Assert.Equal("d", created.Description);
        Assert.Equal(DateTimeKind.Utc, created.CreatedAt.ToDateTime().Kind);

        var fetched = await _client.GetBoardAsync(new GetBoardRequest { Id = created.Id });
        Assert.Equal("Sprint", fetched.Name);
        // The stored timestamp must survive the SQLite round trip unchanged.
        Assert.Equal(created.CreatedAt.ToDateTime(), fetched.CreatedAt.ToDateTime());
    }

    [Fact]
    public async Task ListBoards_TwoBoardsCreated_ReturnsBoth()
    {
        await _client.CreateBoardAsync(new CreateBoardRequest { Name = "A" });
        await _client.CreateBoardAsync(new CreateBoardRequest { Name = "B" });

        var list = await _client.ListBoardsAsync(new ListBoardsRequest());

        Assert.Equal(new[] { "A", "B" }, list.Boards.Select(b => b.Name).OrderBy(n => n));
    }

    [Fact]
    public async Task UpdateBoard_OnlyNameSet_KeepsDescription()
    {
        var created = await _client.CreateBoardAsync(new CreateBoardRequest { Name = "old", Description = "keep" });

        var updated = await _client.UpdateBoardAsync(new UpdateBoardRequest { Id = created.Id, Name = "new" });

        Assert.Equal("new", updated.Name);
        Assert.Equal("keep", updated.Description);
        Assert.Equal("keep", (await _client.GetBoardAsync(new GetBoardRequest { Id = created.Id })).Description);
    }

    [Fact]
    public async Task DeleteBoard_ExistingBoard_RemovesItAndSubsequentGetIsNotFound()
    {
        var created = await _client.CreateBoardAsync(new CreateBoardRequest { Name = "x" });

        await _client.DeleteBoardAsync(new DeleteBoardRequest { Id = created.Id });

        await AssertRpcError(() => _client.GetBoardAsync(new GetBoardRequest { Id = created.Id }).ResponseAsync, StatusCode.NotFound);
    }

    [Fact]
    public async Task GetBoard_UnknownId_ReturnsNotFound()
    {
        await AssertRpcError(() => _client.GetBoardAsync(new GetBoardRequest { Id = 999 }).ResponseAsync, StatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateBoard_UnknownId_ReturnsNotFound()
    {
        await AssertRpcError(() => _client.UpdateBoardAsync(new UpdateBoardRequest { Id = 999, Name = "x" }).ResponseAsync, StatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteBoard_UnknownId_ReturnsNotFound()
    {
        await AssertRpcError(() => _client.DeleteBoardAsync(new DeleteBoardRequest { Id = 999 }).ResponseAsync, StatusCode.NotFound);
    }
}
