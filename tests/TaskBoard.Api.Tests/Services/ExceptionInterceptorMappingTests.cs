using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TaskBoard.Api.Interceptors;

namespace TaskBoard.Api.Tests.Services;

// Calls the interceptor directly with a handler that throws. The mapping depends on the call's
// cancellation token, which is awkward to trigger reliably through a real client, and the
// concurrency conflict needs two overlapping requests to reproduce.
public class ExceptionInterceptorMappingTests
{
    private readonly ExceptionInterceptor _interceptor = new(NullLogger<ExceptionInterceptor>.Instance);

    private Task<string> Invoke(Exception toThrow, CancellationToken callToken = default) =>
        _interceptor.UnaryServerHandler<string, string>(
            "request",
            new FakeServerCallContext(callToken),
            (_, _) => throw toThrow);

    [Fact]
    public async Task OperationCanceled_WhenTheCallWasCancelled_BecomesCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var ex = await Assert.ThrowsAsync<RpcException>(() => Invoke(new OperationCanceledException(), cts.Token));

        Assert.Equal(StatusCode.Cancelled, ex.StatusCode);
    }

    [Fact]
    public async Task OperationCanceled_WhenTheCallWasNotCancelled_IsStillInternal()
    {
        // Not the client hanging up (e.g. a dependency's own timeout): that is a server-side problem.
        var ex = await Assert.ThrowsAsync<RpcException>(() => Invoke(new OperationCanceledException()));

        Assert.Equal(StatusCode.Internal, ex.StatusCode);
    }

    [Fact]
    public async Task ConcurrencyConflict_BecomesAborted()
    {
        var ex = await Assert.ThrowsAsync<RpcException>(() => Invoke(new DbUpdateConcurrencyException("0 rows affected")));

        Assert.Equal(StatusCode.Aborted, ex.StatusCode);
        Assert.DoesNotContain("rows affected", ex.Status.Detail);
    }

    [Fact]
    public async Task RpcException_PassesThroughAsTheSameInstance()
    {
        var original = new RpcException(new Status(StatusCode.NotFound, "nope"));

        var ex = await Assert.ThrowsAsync<RpcException>(() => Invoke(original));

        Assert.Same(original, ex);
    }

    // The interceptor only reads Method and CancellationToken; everything else is irrelevant here.
    private sealed class FakeServerCallContext : ServerCallContext
    {
        private readonly CancellationToken _token;

        public FakeServerCallContext(CancellationToken token) => _token = token;

        protected override string MethodCore => "/test.v1.Service/Method";
        protected override string HostCore => "localhost";
        protected override string PeerCore => "test";
        protected override DateTime DeadlineCore => DateTime.MaxValue;
        protected override Metadata RequestHeadersCore => [];
        protected override CancellationToken CancellationTokenCore => _token;
        protected override Metadata ResponseTrailersCore => [];
        protected override Status StatusCore { get; set; }
        protected override WriteOptions? WriteOptionsCore { get; set; }
        protected override AuthContext AuthContextCore => new(null, new Dictionary<string, List<AuthProperty>>());

        protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options) =>
            throw new NotSupportedException();

        protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders) => Task.CompletedTask;
    }
}
