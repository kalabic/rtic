using DotBase.Log;

namespace LibRTIC.MiniTaskLib;


public interface IEventContainer
{
    public IEventContainer NewCompatibleInstance();
}

public class EventContainer<TMessage> : IEventContainer
{
    private InfoLog? _info = null;

    /// <summary>
    /// Difference between async and normal event handlers is that async handlers are wrapped up into
    /// a task and will be invoked first. See <see cref="EventCollection.ConnectAsync"/>
    /// </summary>
    private event EventHandler<TMessage>? _asyncEvent;

    private event EventHandler<TMessage>? _event;

    public EventContainer(InfoLog? info = null)
    {
        _info = info;
    }

    /// <summary>
    /// TODO: Improve exception handling.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="update"></param>
    public void Invoke(object? sender, TMessage update)
    {
        // TODO: Exceptions from inside invoked event handlers are dangerous and break everything.
        try
        {
            _asyncEvent?.Invoke(sender, update);
            _event?.Invoke(sender, update);
        }
        catch (Exception ex)
        {
            _info?.Warning("Exception while invoking event handlers.", ex);
        }
    }

    /// <summary>
    /// No difference between async and normal event handlers except all async event handlers will be invoked first.
    /// </summary>
    /// <param name="eventHandler"></param>
    public void ConnectEventHandlerAsync(EventHandler<TMessage> eventHandler)
    {
        this._asyncEvent += eventHandler;
    }

    public void ConnectEventHandler(EventHandler<TMessage> eventHandler)
    {
        this._event += eventHandler;
    }

    public void ConnectEventHandler(EventContainer<TMessage> eventHandler)
    {
        this._asyncEvent += eventHandler._asyncEvent;
        this._event += eventHandler._event;
    }
    public void DisconnectEventHandlerAsync(EventHandler<TMessage> eventHandler)
    {
        this._asyncEvent -= eventHandler;
    }

    public void DisconnectEventHandler(EventHandler<TMessage> eventHandler)
    {
        this._event -= eventHandler;
    }

    public void DisconnectEventHandler(EventContainer<TMessage> eventHandler)
    {
        this._asyncEvent -= eventHandler._asyncEvent;
        this._event -= eventHandler._event;
    }

    public IEventContainer NewCompatibleInstance()
    {
        return new EventContainer<TMessage>(_info);
    }
}
