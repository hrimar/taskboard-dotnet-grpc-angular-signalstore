using Grpc.Core;
using TaskBoard.Contracts.V1;

namespace TaskBoard.Api.Tests.Services;

public class LabelGrpcServiceTests : GrpcTestBase
{
    private readonly LabelService.LabelServiceClient _client;

    public LabelGrpcServiceTests() => _client = new LabelService.LabelServiceClient(Channel);

    [Fact]
    public async Task CreateLabel_ValidRequest_ReturnsLabelThatCanBeFetched()
    {
        var created = await _client.CreateLabelAsync(new CreateLabelRequest { Name = "Bug", Color = "#FF0000" });

        var fetched = await _client.GetLabelAsync(new GetLabelRequest { Id = created.Id });

        Assert.True(created.Id > 0);
        Assert.Equal("Bug", fetched.Name);
        Assert.Equal("#FF0000", fetched.Color);
    }

    [Fact]
    public async Task ListLabels_TwoLabelsCreated_ReturnsBoth()
    {
        await _client.CreateLabelAsync(new CreateLabelRequest { Name = "A", Color = "#000000" });
        await _client.CreateLabelAsync(new CreateLabelRequest { Name = "B", Color = "#FFFFFF" });

        var list = await _client.ListLabelsAsync(new ListLabelsRequest());

        Assert.Equal(2, list.Labels.Count);
    }

    [Fact]
    public async Task UpdateLabel_ExistingLabel_ReplacesNameAndColor()
    {
        var created = await _client.CreateLabelAsync(new CreateLabelRequest { Name = "a", Color = "#000000" });

        var updated = await _client.UpdateLabelAsync(new UpdateLabelRequest { Id = created.Id, Name = "b", Color = "#111111" });

        Assert.Equal("b", updated.Name);
        Assert.Equal("#111111", (await _client.GetLabelAsync(new GetLabelRequest { Id = created.Id })).Color);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateLabel_BlankName_ReturnsInvalidArgument(string name)
    {
        await AssertRpcError(() => _client.CreateLabelAsync(new CreateLabelRequest { Name = name, Color = "#000000" }).ResponseAsync, StatusCode.InvalidArgument);
    }

    [Fact]
    public async Task UpdateLabel_BlankName_ReturnsInvalidArgumentAndKeepsOldName()
    {
        var created = await _client.CreateLabelAsync(new CreateLabelRequest { Name = "a", Color = "#000000" });

        await AssertRpcError(() => _client.UpdateLabelAsync(new UpdateLabelRequest { Id = created.Id, Name = "", Color = "#111111" }).ResponseAsync, StatusCode.InvalidArgument);

        Assert.Equal("a", (await _client.GetLabelAsync(new GetLabelRequest { Id = created.Id })).Name);
    }

    [Fact]
    public async Task DeleteLabel_ExistingLabel_RemovesIt()
    {
        var created = await _client.CreateLabelAsync(new CreateLabelRequest { Name = "a", Color = "#000000" });

        await _client.DeleteLabelAsync(new DeleteLabelRequest { Id = created.Id });

        await AssertRpcError(() => _client.GetLabelAsync(new GetLabelRequest { Id = created.Id }).ResponseAsync, StatusCode.NotFound);
    }

    [Fact]
    public async Task GetLabel_UnknownId_ReturnsNotFound()
    {
        await AssertRpcError(() => _client.GetLabelAsync(new GetLabelRequest { Id = 999 }).ResponseAsync, StatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateLabel_UnknownId_ReturnsNotFound()
    {
        await AssertRpcError(() => _client.UpdateLabelAsync(new UpdateLabelRequest { Id = 999, Name = "x", Color = "#000000" }).ResponseAsync, StatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteLabel_UnknownId_ReturnsNotFound()
    {
        await AssertRpcError(() => _client.DeleteLabelAsync(new DeleteLabelRequest { Id = 999 }).ResponseAsync, StatusCode.NotFound);
    }
}
