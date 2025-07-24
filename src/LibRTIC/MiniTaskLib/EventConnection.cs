namespace LibRTIC.MiniTaskLib;

public abstract class EventConnectionInstance : IDisposable
{
#if DEBUG_UNDISPOSED
    public static int UNDISPOSED_COUNT = 0;

    public static int INSTANCE_COUNT = 0;

    protected bool _disposed = false;
#endif

    public abstract void Dispose();
}

public abstract class EventConnection<TMessage> : EventConnectionInstance
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

public class EventHandlerConnection<TMessage> : EventConnection<TMessage>
{
    EventHandler<TMessage> _otherHandler;

    public EventHandlerConnection(EventContainer<TMessage> item, EventHandler<TMessage> otherHandler)
        : base(item)
    {
        this._otherHandler = otherHandler;
    }

    protected override void Disconnect()
    {
        _item?.DisconnectEventHandler(_otherHandler);
    }
}

public class EventAsyncConnection<TMessage> : EventConnection<TMessage>
{
    EventHandler<TMessage> _otherHandler;

    public EventAsyncConnection(EventContainer<TMessage> item, EventHandler<TMessage> otherHandler)
        : base(item)
    {
        this._otherHandler = otherHandler;
    }

    protected override void Disconnect()
    {
        _item?.DisconnectEventHandlerAsync(_otherHandler);
    }
}

public class EventContainerConnection<TMessage> : EventConnection<TMessage>
{
    EventContainer<TMessage> _otherContainer;

    public EventContainerConnection(EventContainer<TMessage> item, EventContainer<TMessage> otherContainer)
        : base(item)
    {
        this._otherContainer = otherContainer;
    }

    protected override void Disconnect()
    {
        _item?.DisconnectEventHandler(_otherContainer);
    }
}
