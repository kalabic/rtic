using DotBase.Core;
using DotBase.Event;

namespace LibRTIC.MiniTaskLib;

internal abstract class EventConnectionBase : DisposableBase
{
#if DEBUG_UNDISPOSED
    public static int UNDISPOSED_COUNT = 0;

    public static int INSTANCE_COUNT = 0;

    private bool _disposed = false;
#endif

    protected EventConnectionBase()
    {
#if DEBUG_UNDISPOSED
        Interlocked.Increment(ref UNDISPOSED_COUNT);
        Interlocked.Increment(ref INSTANCE_COUNT);
#endif
    }

#if DEBUG_UNDISPOSED
    ~EventConnectionBase()
    {
        Dispose(false);
        Interlocked.Decrement(ref INSTANCE_COUNT);
    }
#endif

    protected override void Dispose(bool disposing)
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

        base.Dispose(disposing);
    }
}

internal abstract class EventConnection<TMessage> : EventConnectionBase
{
    protected EventContainer<TMessage>? _sourceEvent;

    protected EventConnection(EventContainer<TMessage> sourceEvent)
    {
        _sourceEvent = sourceEvent;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Disconnect();
            _sourceEvent = null;
        }

        base.Dispose(disposing);
    }

    protected abstract void Disconnect();
}

internal sealed class EventHandlerConnection<TMessage> : EventConnection<TMessage>
{
    private readonly EventHandler<TMessage> _handler;

    public EventHandlerConnection(EventContainer<TMessage> sourceEvent, EventHandler<TMessage> handler)
        : base(sourceEvent)
    {
        _handler = handler;
    }

    protected override void Disconnect()
    {
        _sourceEvent?.RemoveHandler(_handler);
    }
}

internal sealed class AsyncHandlerConnection<TMessage> : EventConnection<TMessage>
{
    private readonly EventHandler<TMessage> _handler;

    public AsyncHandlerConnection(EventContainer<TMessage> sourceEvent, EventHandler<TMessage> handler)
        : base(sourceEvent)
    {
        _handler = handler;
    }

    protected override void Disconnect()
    {
        _sourceEvent?.RemoveHandlerAsync(_handler);
    }
}

internal sealed class EventForwardingConnection<TMessage> : EventConnection<TMessage>
{
    private readonly EventContainer<TMessage> _targetEvent;

    public EventForwardingConnection(EventContainer<TMessage> sourceEvent, EventContainer<TMessage> targetEvent)
        : base(sourceEvent)
    {
        _targetEvent = targetEvent;
    }

    protected override void Disconnect()
    {
        _sourceEvent?.Disconnect(_targetEvent);
    }
}
