using Grpc.Core;
using Grpc.Core.Interceptors;

namespace TaskBoard.Api.Interceptors;

// The services signal expected failures with RpcException (NotFound, InvalidArgument, ...) and
// those pass through untouched. Anything else is a bug or an infrastructure failure: without this
// interceptor the client would get Unknown plus the raw exception text (stack details, SQL, paths).
public sealed class ExceptionInterceptor : Interceptor
{
    private readonly ILogger<ExceptionInterceptor> _logger;

    public ExceptionInterceptor(ILogger<ExceptionInterceptor> logger)
    {
        _logger = logger;
    }

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        try
        {
            return await continuation(request, context);
        }
        catch (Exception ex) when (ex is not RpcException)
        {
            throw Translate(ex, context);
        }
    }

    public override async Task ServerStreamingServerHandler<TRequest, TResponse>(
        TRequest request,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        ServerStreamingServerMethod<TRequest, TResponse> continuation)
    {
        try
        {
            await continuation(request, responseStream, context);
        }
        catch (Exception ex) when (ex is not RpcException)
        {
            throw Translate(ex, context);
        }
    }

    private RpcException Translate(Exception ex, ServerCallContext context)
    {
        // The client hanging up (or its deadline passing) cancels the EF query and surfaces here as
        // an OperationCanceledException. That is not a server error and must not be logged as one.
        if (ex is OperationCanceledException && context.CancellationToken.IsCancellationRequested)
            return new RpcException(new Status(StatusCode.Cancelled, "The call was cancelled."));

        _logger.LogError(ex, "Unhandled exception in gRPC call {Method}", context.Method);

        // Generic on purpose: the details are in the log, not for the caller.
        return new RpcException(new Status(StatusCode.Internal, "An unexpected error occurred."));
    }
}
