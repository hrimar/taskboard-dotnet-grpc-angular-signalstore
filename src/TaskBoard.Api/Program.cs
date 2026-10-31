using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using TaskBoard.Api.Interceptors;
using TaskBoard.Api.Services;
using TaskBoard.Api.Streaming;
using TaskBoard.Infrastructure.Persistence;

const string AngularDevClient = "AngularDevClient";

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Interceptors run in registration order, first = outermost: Logging wraps Exception, so it logs
// the status the client actually receives (after unexpected exceptions became Internal).
builder.Services.AddGrpc(options =>
{
    options.Interceptors.Add<LoggingInterceptor>();
    options.Interceptors.Add<ExceptionInterceptor>();
})
// Serves the RPCs that carry a google.api.http option (see the .proto files) also as plain REST/JSON,
// e.g. GET /v1/boards/5. It calls the very same service classes through the same gRPC pipeline,
// so the interceptors, validation and status codes apply unchanged (NotFound -> 404, ...).
.AddJsonTranscoding();

// Swagger describes REST routes, which is why it needs the transcoding above: a plain gRPC service
// has no URL/verb/JSON shape to document. AddGrpcSwagger reads the transcoded routes. It is
// Development-only as a whole (services here, middleware below): outside Development it would
// reflect over every endpoint for a page nobody can open. Transcoding itself stays on everywhere.
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddGrpcSwagger();
    builder.Services.AddSwaggerGen(options =>
        options.SwaggerDoc("v1", new OpenApiInfo { Title = "TaskBoard API", Version = "v1" }));
}

// Singleton, not scoped: the open WatchBoard streams and the services that publish events
// live in different requests but must share one subscriber list.
builder.Services.AddSingleton<IBoardEventBroadcaster, BoardEventBroadcaster>();

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

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "TaskBoard API v1"));
}

// Configure the HTTP request pipeline.
app.MapGrpcService<LabelGrpcService>();
app.MapGrpcService<BoardGrpcService>();
app.MapGrpcService<TaskItemGrpcService>();
app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

app.Run();

// Top-level statements generate an internal Program class; this makes it visible to
// WebApplicationFactory<Program> in the integration tests.
public partial class Program;
