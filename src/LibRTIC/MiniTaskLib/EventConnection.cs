namespace LibRTIC.MiniTaskLib;

public abstract class EventConnectionInstance : IDisposable
{
    public abstract void Dispose();
}

public abstract class EventConnection<TMessage> : EventConnectionInstance
{
    protected EventContainer<TMessage>? _item;

    public EventConnection(EventContainer<TMessage> item)
    {
        this._item = item;
    }

    public override void Dispose()
    {
        Dispose(true);
    }

    protected void Dispose(bool disposing)
    {
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
