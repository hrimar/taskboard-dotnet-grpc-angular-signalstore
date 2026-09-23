using Microsoft.EntityFrameworkCore;
using TaskBoard.Api.Services;
using TaskBoard.Infrastructure.Persistence;

const string AngularDevClient = "AngularDevClient";

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddGrpc();

builder.Services.AddDbContext<TaskBoardDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("TaskBoard")));

// The Angular client runs on a different origin than the API, so the browser enforces CORS.
// The allowed origin(s) come from configuration (appsettings.Development.json for "ng serve"
// on localhost:4200; a real deployment would set its own origin via appsettings.Production.json
// or an environment variable) instead of being hardcoded here.
// gRPC-Web additionally relies on custom response headers the browser would otherwise hide
// from the JS client, so they must be explicitly exposed - without this, every call fails
// with an unreadable status.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy(AngularDevClient, policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .WithExposedHeaders("Grpc-Status", "Grpc-Message", "Grpc-Encoding");
    });
});

var app = builder.Build();

app.UseCors(AngularDevClient);

// Unwraps gRPC-Web requests (browser-compatible HTTP/1.1 framing) back into the plain
// gRPC calls our services already implement - the service code itself needs no changes.
app.UseGrpcWeb(new GrpcWebOptions { DefaultEnabled = true });

// Configure the HTTP request pipeline.
app.MapGrpcService<GreeterService>();
app.MapGrpcService<LabelGrpcService>();
app.MapGrpcService<BoardGrpcService>();
app.MapGrpcService<TaskItemGrpcService>();
app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

app.Run();
