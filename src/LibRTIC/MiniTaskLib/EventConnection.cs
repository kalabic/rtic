using DotBase.Event;

namespace LibRTIC.MiniTaskLib;

internal abstract class EventConnectionBase : IDisposable
{
#if DEBUG_UNDISPOSED
    public static int UNDISPOSED_COUNT = 0;

    public static int INSTANCE_COUNT = 0;

    protected bool _disposed = false;
#endif

    public abstract void Dispose();
}

internal abstract class EventConnection<TMessage> : EventConnectionBase
{
    protected EventContainer<TMessage>? _item;

    public EventConnection(EventContainer<TMessage> item)
    {
        this._item = item;
#if DEBUG_UNDISPOSED
        Interlocked.Increment(ref UNDISPOSED_COUNT);
        Interlocked.Increment(ref INSTANCE_COUNT);
#endif
    }

#if DEBUG_UNDISPOSED
    ~EventConnection()
    {
        Dispose(false);
        Interlocked.Decrement(ref INSTANCE_COUNT);
    }
#endif

    public override void Dispose()
    {
        Dispose(true);
    }

    protected void Dispose(bool disposing)
    {
#if DEBUG_UNDISPOSED
        if (!_disposed && !disposing)
        {
            throw new InvalidOperationException("Not disposed properly.");
        }
        if (!_disposed)
        {
            _disposed = true;
            Interlocked.Decrement(ref UNDISPOSED_COUNT);
        }
#endif

        // Release managed resources.
        if (disposing)
        {
            Disconnect();
            _item = null;
        }
        // Release unmanaged resources.
    }

    protected abstract void Disconnect();
}

internal class EventHandlerConnection<TMessage> : EventConnection<TMessage>
{
    EventHandler<TMessage> _otherHandler;

    public EventHandlerConnection(EventContainer<TMessage> item, EventHandler<TMessage> otherHandler)
        : base(item)
    {
        this._otherHandler = otherHandler;
    }

    protected override void Disconnect()
    {
        _item?.RemoveHandler(_otherHandler);
    }
}

internal class AsyncHandlerConnection<TMessage> : EventConnection<TMessage>
{
    EventHandler<TMessage> _otherHandler;

    public AsyncHandlerConnection(EventContainer<TMessage> item, EventHandler<TMessage> otherHandler)
        : base(item)
    {
        this._otherHandler = otherHandler;
    }

    protected override void Disconnect()
    {
        _item?.RemoveHandlerAsync(_otherHandler);
    }
}

internal class EventForwardingConnection<TMessage> : EventConnection<TMessage>
{
    EventContainer<TMessage> _otherContainer;

    public EventForwardingConnection(EventContainer<TMessage> item, EventContainer<TMessage> otherContainer)
        : base(item)
    {
        this._otherContainer = otherContainer;
    }

    protected override void Disconnect()
    {
        _item?.Disconnect(_otherContainer);
    }
}
