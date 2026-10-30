using System.Diagnostics;
using Grpc.Core;
using Grpc.Core.Interceptors;

namespace TaskBoard.Api.Interceptors;

// Registered before ExceptionInterceptor, so it is the outer one: by the time an error reaches
// it, it is already the final RpcException the client will see - the logged status is the real one.
public sealed class LoggingInterceptor : Interceptor
{
    private readonly ILogger<LoggingInterceptor> _logger;

    public LoggingInterceptor(ILogger<LoggingInterceptor> logger)
    {
        _logger = logger;
    }

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        var started = Stopwatch.GetTimestamp();
        var status = StatusCode.OK;
        try
        {
            return await continuation(request, context);
        }
        catch (RpcException ex)
        {
            status = ex.StatusCode;
            throw;
        }
        catch (Exception)
        {
            // Only reachable if ExceptionInterceptor is removed or reordered; gRPC reports such an
            // exception to the client as Unknown, so log that instead of a misleading OK.
            status = StatusCode.Unknown;
            throw;
        }
        finally
        {
            Log(context.Method, status, started);
        }
    }

    // For a stream this logs once, when the stream ends - not per event, WatchBoard would flood the log.
    public override async Task ServerStreamingServerHandler<TRequest, TResponse>(
        TRequest request,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        ServerStreamingServerMethod<TRequest, TResponse> continuation)
    {
        var started = Stopwatch.GetTimestamp();
        var status = StatusCode.OK;
        try
        {
            await continuation(request, responseStream, context);
        }
        catch (RpcException ex)
        {
            status = ex.StatusCode;
            throw;
        }
        catch (Exception)
        {
            status = StatusCode.Unknown; // see UnaryServerHandler
            throw;
        }
        finally
        {
            Log(context.Method, status, started);
        }
    }

    private void Log(string method, StatusCode status, long started)
    {
        var elapsed = Stopwatch.GetElapsedTime(started);

        // Client mistakes (NotFound, InvalidArgument, ...) are normal traffic, not server problems.
        var level = status is StatusCode.Internal or StatusCode.Unknown or StatusCode.DataLoss
            ? LogLevel.Error
            : LogLevel.Information;

        _logger.Log(level, "gRPC {Method} finished with {Status} in {ElapsedMs:0.0} ms", method, status, elapsed.TotalMilliseconds);
    }
}
