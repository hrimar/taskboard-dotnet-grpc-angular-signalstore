using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace TaskBoard.Api.Tests.Services;

// Plain HttpClient, no gRPC client: this is what a curl user or a Swagger page does. The point is
// that the routes from the google.api.http options reach the SAME service code, so validation and
// status-code behaviour carry over (NotFound -> 404, InvalidArgument -> 400).
public class RestTranscodingTests : IDisposable
{
    private readonly TaskBoardApiFactory _factory = new();
    private readonly HttpClient _http;

    public RestTranscodingTests()
    {
        _http = _factory.CreateClient();
    }

    private static JsonElement Json(HttpResponseMessage response) =>
        JsonDocument.Parse(response.Content.ReadAsStringAsync().Result).RootElement;

    private async Task<int> NewBoardId()
    {
        var response = await _http.PostAsJsonAsync("/v1/boards", new { name = "b", description = "d" });
        response.EnsureSuccessStatusCode();
        return Json(response).GetProperty("id").GetInt32();
    }

    [Fact]
    public async Task Board_CreateThenGet_RoundTripsAsCamelCaseJson()
    {
        var id = await NewBoardId();

        var response = await _http.GetAsync($"/v1/boards/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var board = Json(response);
        Assert.Equal("b", board.GetProperty("name").GetString());
        // proto field created_at appears as createdAt, and a Timestamp as an ISO-8601 string.
        Assert.True(DateTime.TryParse(board.GetProperty("createdAt").GetString(), out _));
    }

    [Fact]
    public async Task GetBoard_UnknownId_Returns404WithTheGrpcMessage()
    {
        var response = await _http.GetAsync("/v1/boards/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Board 999 not found.", Json(response).GetProperty("message").GetString());
    }

    [Fact]
    public async Task CreateBoard_BlankName_Returns400()
    {
        var response = await _http.PostAsJsonAsync("/v1/boards", new { name = "  " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateBoard_Patch_ChangesOnlyTheFieldsInTheBody()
    {
        var id = await NewBoardId();

        var response = await _http.PatchAsJsonAsync($"/v1/boards/{id}", new { name = "renamed" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var board = Json(response);
        Assert.Equal("renamed", board.GetProperty("name").GetString());
        Assert.Equal("d", board.GetProperty("description").GetString());
    }

    [Fact]
    public async Task DeleteBoard_Then_Get_Returns404()
    {
        var id = await NewBoardId();

        var deleted = await _http.DeleteAsync($"/v1/boards/{id}");

        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _http.GetAsync($"/v1/boards/{id}")).StatusCode);
    }

    [Fact]
    public async Task Label_PutReplacesNameAndColor()
    {
        var created = await _http.PostAsJsonAsync("/v1/labels", new { name = "a", color = "#000000" });
        var id = Json(created).GetProperty("id").GetInt32();

        var updated = await _http.PutAsJsonAsync($"/v1/labels/{id}", new { name = "b", color = "#111111" });

        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal("b", Json(updated).GetProperty("name").GetString());
        Assert.Equal("#111111", Json(await _http.GetAsync($"/v1/labels/{id}")).GetProperty("color").GetString());
    }

    [Fact]
    public async Task Task_CreateInBoardRoute_UsesPathIdAndEnumNames()
    {
        var boardId = await NewBoardId();

        var response = await _http.PostAsJsonAsync($"/v1/boards/{boardId}/tasks",
            new { title = "T", status = "TASK_ITEM_STATUS_IN_PROGRESS" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var task = Json(response);
        // board_id came from the URL, not from the body; enums travel as their proto names.
        Assert.Equal(boardId, task.GetProperty("boardId").GetInt32());
        Assert.Equal("TASK_ITEM_STATUS_IN_PROGRESS", task.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Task_CreateWithUnknownLabel_Returns404()
    {
        var boardId = await NewBoardId();

        var response = await _http.PostAsJsonAsync($"/v1/boards/{boardId}/tasks", new { title = "T", labelIds = new[] { 999 } });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Label(s) 999 not found.", Json(response).GetProperty("message").GetString());
    }

    [Fact]
    public async Task Task_ListInBoardRoute_ReturnsOnlyThatBoardsTasksWithPagingToken()
    {
        var boardId = await NewBoardId();
        var otherId = await NewBoardId();
        for (var i = 0; i < 3; i++)
            await _http.PostAsJsonAsync($"/v1/boards/{boardId}/tasks", new { title = $"t{i}" });
        await _http.PostAsJsonAsync($"/v1/boards/{otherId}/tasks", new { title = "elsewhere" });

        var response = await _http.GetAsync($"/v1/boards/{boardId}/tasks?pageSize=2");

        var page = Json(response);
        Assert.Equal(2, page.GetProperty("taskItems").GetArrayLength());
        Assert.Equal("2", page.GetProperty("nextPageToken").GetString());

        var all = Json(await _http.GetAsync("/v1/tasks"));
        Assert.Equal(4, all.GetProperty("taskItems").GetArrayLength());
    }

    [Fact]
    public async Task Task_PatchWithUpdateMaskQuery_AppliesOnlyTheMaskedField()
    {
        var boardId = await NewBoardId();
        var created = await _http.PostAsJsonAsync($"/v1/boards/{boardId}/tasks", new { title = "keep" });
        var id = Json(created).GetProperty("id").GetInt32();

        // The body carries two fields, the mask names one: the title must stay untouched.
        var response = await _http.PatchAsJsonAsync($"/v1/tasks/{id}?updateMask=status",
            new { title = "ignored", status = "TASK_ITEM_STATUS_DONE" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var task = Json(response);
        Assert.Equal("TASK_ITEM_STATUS_DONE", task.GetProperty("status").GetString());
        Assert.Equal("keep", task.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Task_PatchWithoutUpdateMask_Returns400()
    {
        var boardId = await NewBoardId();
        var id = Json(await _http.PostAsJsonAsync($"/v1/boards/{boardId}/tasks", new { title = "T" })).GetProperty("id").GetInt32();

        var response = await _http.PatchAsJsonAsync($"/v1/tasks/{id}", new { title = "x" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task WatchBoard_HasNoRestRoute()
    {
        var boardId = await NewBoardId();

        var response = await _http.GetAsync($"/v1/boards/{boardId}/watch");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Swagger_DocumentListsTheRestRoutesButNotTheStream()
    {
        var response = await _http.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var paths = Json(response).GetProperty("paths");
        Assert.True(paths.TryGetProperty("/v1/boards/{id}", out var board));
        Assert.True(board.TryGetProperty("get", out _));
        Assert.True(paths.TryGetProperty("/v1/tasks/{id}", out var task));
        Assert.True(task.TryGetProperty("patch", out _));
        Assert.DoesNotContain("watch", paths.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        _http.Dispose();
        _factory.Dispose();
    }
}
