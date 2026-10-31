using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TaskBoard.Api.Streaming;
using TaskBoard.Contracts.V1;

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

    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    private async Task<int> NewBoardId()
    {
        var response = await _http.PostAsJsonAsync("/v1/boards", new { name = "b", description = "d" });
        response.EnsureSuccessStatusCode();
        return (await Json(response)).GetProperty("id").GetInt32();
    }

    private async Task<int> NewTaskId(int boardId, string title = "T")
    {
        var response = await _http.PostAsJsonAsync($"/v1/boards/{boardId}/tasks", new { title });
        response.EnsureSuccessStatusCode();
        return (await Json(response)).GetProperty("id").GetInt32();
    }

    private async Task<int> NewLabelId(string name = "a")
    {
        var response = await _http.PostAsJsonAsync("/v1/labels", new { name, color = "#000000" });
        response.EnsureSuccessStatusCode();
        return (await Json(response)).GetProperty("id").GetInt32();
    }

    [Fact]
    public async Task Board_CreateThenGet_RoundTripsAsCamelCaseJson()
    {
        var id = await NewBoardId();

        var response = await _http.GetAsync($"/v1/boards/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var board = await Json(response);
        Assert.Equal("b", board.GetProperty("name").GetString());
        // proto field created_at appears as createdAt, and a Timestamp as an ISO-8601 string.
        Assert.True(DateTime.TryParse(board.GetProperty("createdAt").GetString(), out _));
    }

    [Fact]
    public async Task Board_List_ReturnsAllBoardsUnderBoards()
    {
        await NewBoardId();
        await NewBoardId();

        var response = await _http.GetAsync("/v1/boards");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, (await Json(response)).GetProperty("boards").GetArrayLength());
    }

    [Fact]
    public async Task GetBoard_UnknownId_Returns404WithTheGrpcMessage()
    {
        var response = await _http.GetAsync("/v1/boards/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Board 999 not found.", (await Json(response)).GetProperty("message").GetString());
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
        var board = await Json(response);
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
        var id = await NewLabelId();

        var updated = await _http.PutAsJsonAsync($"/v1/labels/{id}", new { name = "b", color = "#111111" });

        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal("b", (await Json(updated)).GetProperty("name").GetString());
        Assert.Equal("#111111", (await Json(await _http.GetAsync($"/v1/labels/{id}"))).GetProperty("color").GetString());
    }

    [Fact]
    public async Task Label_ListAndDelete()
    {
        var keep = await NewLabelId("keep");
        var drop = await NewLabelId("drop");

        var list = await _http.GetAsync("/v1/labels");
        Assert.Equal(2, (await Json(list)).GetProperty("labels").GetArrayLength());

        Assert.Equal(HttpStatusCode.OK, (await _http.DeleteAsync($"/v1/labels/{drop}")).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await _http.GetAsync($"/v1/labels/{drop}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _http.GetAsync($"/v1/labels/{keep}")).StatusCode);
    }

    [Fact]
    public async Task Task_CreateInBoardRoute_UsesPathIdAndEnumNames()
    {
        var boardId = await NewBoardId();

        var response = await _http.PostAsJsonAsync($"/v1/boards/{boardId}/tasks",
            new { title = "T", status = "TASK_ITEM_STATUS_IN_PROGRESS" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var task = await Json(response);
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
        Assert.Equal("Label(s) 999 not found.", (await Json(response)).GetProperty("message").GetString());
    }

    [Fact]
    public async Task Task_GetAndDelete_ByIdRoute()
    {
        var id = await NewTaskId(await NewBoardId(), "find me");

        var found = await _http.GetAsync($"/v1/tasks/{id}");
        Assert.Equal(HttpStatusCode.OK, found.StatusCode);
        Assert.Equal("find me", (await Json(found)).GetProperty("title").GetString());

        Assert.Equal(HttpStatusCode.OK, (await _http.DeleteAsync($"/v1/tasks/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _http.GetAsync($"/v1/tasks/{id}")).StatusCode);
    }

    [Fact]
    public async Task Task_ListInBoardRoute_ReturnsOnlyThatBoardsTasksWithPagingToken()
    {
        var boardId = await NewBoardId();
        var otherId = await NewBoardId();
        for (var i = 0; i < 3; i++)
            await NewTaskId(boardId, $"t{i}");
        await NewTaskId(otherId, "elsewhere");

        var response = await _http.GetAsync($"/v1/boards/{boardId}/tasks?pageSize=2");

        var page = await Json(response);
        Assert.Equal(2, page.GetProperty("taskItems").GetArrayLength());
        Assert.Equal("2", page.GetProperty("nextPageToken").GetString());

        // The second route has no board_id, so it arrives as 0 = every board.
        var all = await Json(await _http.GetAsync("/v1/tasks"));
        Assert.Equal(4, all.GetProperty("taskItems").GetArrayLength());
    }

    [Fact]
    public async Task Task_PatchWithUpdateMaskQuery_AppliesOnlyTheMaskedField()
    {
        var id = await NewTaskId(await NewBoardId(), "keep");

        // The body carries two fields, the mask names one: the title must stay untouched.
        var response = await _http.PatchAsJsonAsync($"/v1/tasks/{id}?updateMask=status",
            new { title = "ignored", status = "TASK_ITEM_STATUS_DONE" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var task = await Json(response);
        Assert.Equal("TASK_ITEM_STATUS_DONE", task.GetProperty("status").GetString());
        Assert.Equal("keep", task.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Task_PatchWithoutUpdateMask_Returns400()
    {
        var id = await NewTaskId(await NewBoardId());

        var response = await _http.PatchAsJsonAsync($"/v1/tasks/{id}", new { title = "x" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // JSON names mask paths in camelCase (labelIds, dueDate) while the service compares snake_case
    // (label_ids, due_date). This pins that the transcoder converts them before the service sees them.
    [Fact]
    public async Task Task_PatchWithCamelCaseMaskPaths_AppliesMultiWordFields()
    {
        var id = await NewTaskId(await NewBoardId());
        var labelId = await NewLabelId();

        var response = await _http.PatchAsJsonAsync($"/v1/tasks/{id}?updateMask=labelIds,dueDate",
            new { labelIds = new[] { labelId }, dueDate = "2031-01-02T00:00:00Z" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var task = await Json(response);
        Assert.Equal(labelId, task.GetProperty("labelIds")[0].GetInt32());
        Assert.StartsWith("2031-01-02", task.GetProperty("dueDate").GetString());
    }

    // Documented in taskitem.proto: the task that is updated is the one in the path.
    [Fact]
    public async Task Task_PatchWithMismatchingIdInBody_UpdatesTheTaskFromThePath()
    {
        var boardId = await NewBoardId();
        var target = await NewTaskId(boardId, "target");
        var other = await NewTaskId(boardId, "other");

        var response = await _http.PatchAsJsonAsync($"/v1/tasks/{target}?updateMask=title", new { id = other, title = "renamed" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await Json(response);
        Assert.Equal(target, updated.GetProperty("id").GetInt32());
        Assert.Equal("renamed", updated.GetProperty("title").GetString());
        Assert.Equal("other", (await Json(await _http.GetAsync($"/v1/tasks/{other}"))).GetProperty("title").GetString());
    }

    [Fact]
    public async Task WatchBoard_HasNoRestRoute()
    {
        var boardId = await NewBoardId();

        var response = await _http.GetAsync($"/v1/boards/{boardId}/watch");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // Swagger is mapped only in Development; TaskBoardApiFactory sets that environment explicitly.
    [Fact]
    public async Task Swagger_InDevelopment_ListsTheRestRoutesButNotTheStream()
    {
        var response = await _http.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var paths = (await Json(response)).GetProperty("paths");
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

// Outside Development the REST API stays on (it is the gRPC API's twin) but the Swagger page and
// document must not be served.
public class RestInProductionTests : IDisposable
{
    private readonly TaskBoardApiFactory _factory = new();
    private readonly WebApplicationFactory<Program> _production;
    private readonly HttpClient _http;

    public RestInProductionTests()
    {
        _production = _factory.WithWebHostBuilder(builder => builder.UseEnvironment("Production"));
        _http = _production.CreateClient();
    }

    [Fact]
    public async Task Swagger_IsNotServed()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _http.GetAsync("/swagger/v1/swagger.json")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _http.GetAsync("/swagger/index.html")).StatusCode);
    }

    [Fact]
    public async Task RestRoutes_StillWork()
    {
        var response = await _http.PostAsJsonAsync("/v1/boards", new { name = "prod" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    public void Dispose()
    {
        _http.Dispose();
        _production.Dispose();
        _factory.Dispose();
    }
}

// The README says Aborted becomes HTTP 409. A concurrency conflict is the one real source of
// Aborted; the broadcaster is only borrowed to make a handler throw it after the task was saved.
public class RestStatusMappingTests : IDisposable
{
    private readonly TaskBoardApiFactory _factory = new();
    private readonly WebApplicationFactory<Program> _failing;
    private readonly HttpClient _http;

    public RestStatusMappingTests()
    {
        _failing = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.Replace(ServiceDescriptor.Singleton<IBoardEventBroadcaster, ConflictBroadcaster>())));
        _http = _failing.CreateClient();
    }

    [Fact]
    public async Task ConcurrencyConflict_ReturnsHttp409()
    {
        var board = await _http.PostAsJsonAsync("/v1/boards", new { name = "b" });
        var boardId = (await board.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        var response = await _http.PostAsJsonAsync($"/v1/boards/{boardId}/tasks", new { title = "T" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    public void Dispose()
    {
        _http.Dispose();
        _failing.Dispose();
        _factory.Dispose();
    }

    private sealed class ConflictBroadcaster : IBoardEventBroadcaster
    {
        public void Publish(int boardId, BoardEvent boardEvent) => throw new DbUpdateConcurrencyException("0 rows affected");

        public BoardSubscription Subscribe(int boardId) => throw new NotSupportedException();
    }
}
