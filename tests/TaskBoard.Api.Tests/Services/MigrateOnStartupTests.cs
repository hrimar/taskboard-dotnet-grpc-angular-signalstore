using Grpc.Net.Client;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TaskBoard.Contracts.V1;
using TaskBoard.Infrastructure.Persistence;

namespace TaskBoard.Api.Tests.Services;

// Not on TaskBoardApiFactory on purpose: it swaps in an in-memory database built by EnsureCreated,
// which is exactly what would hide a broken migration chain. Here Program.cs runs unchanged
// against a real, empty SQLite file - the same situation as every e2e run.
public sealed class MigrateOnStartupTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"taskboard-migrate-{Guid.NewGuid():N}.db");

    private WebApplicationFactory<Program> CreateFactory(bool migrateOnStartup) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureLogging(l => l.SetMinimumLevel(LogLevel.Warning));
            // UseSetting, not ConfigureAppConfiguration: these must be visible to Program.cs itself.
            builder.UseSetting("ConnectionStrings:TaskBoard", $"Data Source={_dbPath}");
            builder.UseSetting("Database:MigrateOnStartup", migrateOnStartup.ToString());
        });

    [Fact]
    public async Task MigrateOnStartup_FreshDatabaseFile_AppliesAllMigrations()
    {
        using var factory = CreateFactory(migrateOnStartup: true);
        using var channel = GrpcChannel.ForAddress(factory.Server.BaseAddress, new GrpcChannelOptions { HttpClient = factory.CreateClient() });

        using (var scope = factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<TaskBoardDbContext>().Database;
            Assert.NotEmpty(await database.GetAppliedMigrationsAsync());
            Assert.Empty(await database.GetPendingMigrationsAsync());
        }

        // The schema the migrations built must be the one the services expect.
        var board = await new BoardService.BoardServiceClient(channel).CreateBoardAsync(new CreateBoardRequest { Name = "migrated" });
        Assert.True(board.Id > 0);
    }

    [Fact]
    public async Task MigrateOnStartup_FlagOff_LeavesDatabaseUntouched()
    {
        using var factory = CreateFactory(migrateOnStartup: false);
        _ = factory.Server; // starts the host, i.e. runs Program.cs

        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<TaskBoardDbContext>().Database;
        Assert.Empty(await database.GetAppliedMigrationsAsync());
    }

    public void Dispose()
    {
        // Pooled connections keep the file open, and Windows refuses to delete an open file.
        SqliteConnection.ClearAllPools();
        foreach (var suffix in new[] { "", "-shm", "-wal" })
            File.Delete(_dbPath + suffix);
    }
}
