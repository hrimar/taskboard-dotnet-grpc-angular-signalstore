using System.Collections.Concurrent;
using System.Threading.Channels;
using Grpc.Core;
using TaskBoard.Contracts.V1;

namespace TaskBoard.Api.Streaming;

// Fan-out hub between the services that change tasks (publishers) and the open WatchBoard
// streams (subscribers). Registered as a singleton: gRPC services are created per call, but
// every call must see the same set of subscribers.
//
// In-process only - with more than one API instance a real broker (Redis, a message bus)
// would be needed, because an event published on instance A never reaches a stream on B.
public interface IBoardEventBroadcaster
{
    void Publish(int boardId, BoardEvent boardEvent);

    BoardSubscription Subscribe(int boardId);
}

public sealed class BoardEventBroadcaster : IBoardEventBroadcaster
{
    // Bounded on purpose: an unbounded queue behind a stalled client would grow forever.
    private const int MaxQueuedEvents = 256;

    // boardId -> (subscription id -> subscription)
    private readonly ConcurrentDictionary<int, ConcurrentDictionary<Guid, BoardSubscription>> _subscriptions = new();

    public BoardSubscription Subscribe(int boardId)
    {
        var channel = Channel.CreateBounded<BoardEvent>(new BoundedChannelOptions(MaxQueuedEvents)
        {
            SingleReader = true,
            FullMode = BoundedChannelFullMode.Wait
        });

        var id = Guid.NewGuid();
        var subscription = new BoardSubscription(channel, () => Remove(boardId, id));
        _subscriptions.GetOrAdd(boardId, _ => new()).TryAdd(id, subscription);
        return subscription;
    }

    public void Publish(int boardId, BoardEvent boardEvent)
    {
        if (!_subscriptions.TryGetValue(boardId, out var subscribers))
            return;

        foreach (var subscription in subscribers.Values)
        {
            // TryWrite never blocks the request that changed the task. If the queue is full the
            // client is too slow: silently dropping an event would leave its board wrong with no
            // way to notice, so the stream is ended with an error instead. The client reconnects
            // and reloads the board, which is always correct.
            if (!subscription.Writer.TryWrite(boardEvent))
            {
                subscription.Writer.TryComplete(new RpcException(
                    new Status(StatusCode.ResourceExhausted, "Client is too slow to keep up with board events.")));
            }
        }
    }

    private void Remove(int boardId, Guid id)
    {
        if (_subscriptions.TryGetValue(boardId, out var subscribers))
            subscribers.TryRemove(id, out _);
    }
}

// Registered when Subscribe() returns - not on the first read - so no event published between
// the subscribe call and the first MoveNext is lost. Dispose unregisters it; the stream handler
// does that in a `using`, which also runs when the client disconnects.
public sealed class BoardSubscription : IDisposable
{
    private readonly Channel<BoardEvent> _channel;
    private readonly Action _unregister;

    internal BoardSubscription(Channel<BoardEvent> channel, Action unregister)
    {
        _channel = channel;
        _unregister = unregister;
    }

    public ChannelReader<BoardEvent> Reader => _channel.Reader;

    internal ChannelWriter<BoardEvent> Writer => _channel.Writer;

    public void Dispose()
    {
        _unregister();
        _channel.Writer.TryComplete();
    }
}
