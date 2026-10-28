using Grpc.Core;
using Grpc.Net.Client;

namespace TaskBoard.Api.Tests.Services;

public abstract class GrpcTestBase : IDisposable
{
    private readonly TaskBoardApiFactory _factory = new();
    protected GrpcChannel Channel { get; }

    protected GrpcTestBase()
    {
        Channel = _factory.CreateGrpcChannel();
    }

    protected static async Task<RpcException> AssertRpcError(Func<Task> call, StatusCode expected)
    {
        var ex = await Assert.ThrowsAsync<RpcException>(call);
        Assert.Equal(expected, ex.StatusCode);
        return ex;
    }

    public void Dispose()
    {
        Channel.Dispose();
        _factory.Dispose();
    }
}
