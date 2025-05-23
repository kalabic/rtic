using LibRTIC.MiniTaskLib.Base;
using LibRTIC.MiniTaskLib.MessageQueue;
using LibRTIC.MiniTaskLib.Model;

namespace LibRTIC.MiniTaskLib;

public class EventForwarder<TEventArgs> : IInvokeEvent<TEventArgs>
{
    private Info _info;

    private IQueueWriter<IProcessMessage>? _destinationQueue = null;

    private EventHandler<TEventArgs>? _destinationHandler = null;

    private event EventHandler<TEventArgs>? _eventProxy = null;

    public EventForwarder(Info info, IQueueWriter<IProcessMessage> destinationQueue, EventContainer<TEventArgs> destinationHandler)
    {
        this._info = info;
        this._destinationQueue = destinationQueue;
        this._destinationHandler = destinationHandler.Invoke;
        this._eventProxy += _destinationHandler;
    }

    protected override void Disconnect()
    {
        this._eventProxy -= _destinationHandler;
        _destinationQueue = null;
        _destinationHandler = null;
        _eventProxy = null;
    }

    public void WriteToEventQueue(object? sender, TEventArgs ev)
    {
        if ((_destinationQueue is not null) && !_destinationQueue.IsWriterComplete)
        {
            _destinationQueue.Write(new ForwarderMessage<TEventArgs>(this, ev));
        }
    }

    override public void Invoke(TEventArgs ev)
    {
        // TODO: Exceptions here are dangerous and sneaky.
        try
        {
            _eventProxy?.Invoke(null, ev);
        }
        catch (Exception ex)
        {
            _info.ExceptionOccured(ex);
        }
    }
}
