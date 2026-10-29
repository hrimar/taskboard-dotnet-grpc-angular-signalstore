using Grpc.Core;
using TaskBoard.Api.Streaming;
using TaskBoard.Contracts.V1;

namespace TaskBoard.Api.Tests.Services;

// Plain unit tests on the broadcaster: the overflow path needs a subscriber that does not read,
// which is awkward to arrange through a real stream.
public class BoardEventBroadcasterTests
{
    private const int QueueCapacity = 256;

    private static BoardEvent Event(int id) => new() { TaskDeletedId = id };

    [Fact]
    public async Task Publish_WhenSubscriberQueueIsFull_DeliversBufferedEventsThenFailsWithResourceExhausted()
    {
        var broadcaster = new BoardEventBroadcaster();
        using var subscription = broadcaster.Subscribe(boardId: 1);

        // One more than fits: nobody reads, like a client stuck behind a slow connection.
        for (var i = 1; i <= QueueCapacity + 1; i++)
            broadcaster.Publish(1, Event(i));

        // Buffered events are still delivered in order - only then does the stream fail, so the
        // client's reconnect-and-resync starts from a consistent point.
        for (var i = 1; i <= QueueCapacity; i++)
        {
            Assert.True(await subscription.Reader.WaitToReadAsync());
            Assert.True(subscription.Reader.TryRead(out var received));
            Assert.Equal(i, received.TaskDeletedId);
        }

        var error = await Assert.ThrowsAsync<RpcException>(async () => await subscription.Reader.WaitToReadAsync());
        Assert.Equal(StatusCode.ResourceExhausted, error.StatusCode);
    }

    [Fact]
    public async Task Publish_AfterSubscriptionDisposed_IsNotDelivered()
    {
        var broadcaster = new BoardEventBroadcaster();
        var subscription = broadcaster.Subscribe(boardId: 1);
        subscription.Dispose();

        broadcaster.Publish(1, Event(1));

        // Disposing completes the reader; an event that still reached the queue would be readable.
        Assert.False(await subscription.Reader.WaitToReadAsync());
    }
}
