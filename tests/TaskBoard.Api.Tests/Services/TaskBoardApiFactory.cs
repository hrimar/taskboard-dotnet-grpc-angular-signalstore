using Grpc.Net.Client;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TaskBoard.Infrastructure.Persistence;

namespace TaskBoard.Api.Tests.Services;

// One factory (= one host + one private in-memory database) per test class instance, so tests
// never see each other's rows and need no cleanup.
public sealed class TaskBoardApiFactory : WebApplicationFactory<Program>
{
    // An in-memory SQLite database lives only as long as its connection, so the connection is
    // opened once and kept open for the whole host lifetime; every DbContext shares it.
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public TaskBoardApiFactory()
    {
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        // EF logs every SQL statement at Information; that drowns the test output.
        builder.ConfigureLogging(l => l.SetMinimumLevel(LogLevel.Warning));

        builder.ConfigureServices(services =>
        {
            // Remove every registration AddDbContext made, otherwise the file-based SQLite
            // options from Program.cs would still win.
            services.RemoveAll<TaskBoardDbContext>();
            services.RemoveAll<DbContextOptions<TaskBoardDbContext>>();
            services.RemoveAll<DbContextOptions>();

            services.AddDbContext<TaskBoardDbContext>(o => o.UseSqlite(_connection));

            // EnsureCreated builds the schema from the model (same configurations as the
            // migrations) without depending on the migration history.
            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<TaskBoardDbContext>().Database.EnsureCreated();
        });
    }

    public GrpcChannel CreateGrpcChannel() =>
        GrpcChannel.ForAddress(Server.BaseAddress, new GrpcChannelOptions { HttpClient = CreateClient() });

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _connection.Dispose();
    }
}

internal static class ServiceCollectionExtensions
{
    public static void RemoveAll<T>(this IServiceCollection services)
    {
        foreach (var d in services.Where(d => d.ServiceType == typeof(T)).ToList())
            services.Remove(d);
    }
}
