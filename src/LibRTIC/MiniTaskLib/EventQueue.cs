using DotBase.Event;
using DotBase.Log;
using LibRTIC.MiniTaskLib.Model;

namespace LibRTIC.MiniTaskLib;

public class EventQueue : EventProducerCollection
{
    private IEventMailboxWriter? _destinationMailbox = null;

    public EventQueue(InfoLog info, string label, IEventMailboxWriter destinationMailbox)
         : base(info, label)
    {
        _destinationMailbox = destinationMailbox;
    }

    protected override void Dispose(bool disposing)
    {
        // Release managed resources.
        if (disposing)
        {
            _destinationMailbox = null;
        }
        else
        {
            if (_destinationMailbox is not null)
            {
                throw new InvalidOperationException("Not disposed properly.");
            }
        }

        // Release unmanaged resources.
        base.Dispose(disposing);
    }

    public EventContainer<TMessage>? ForwardFrom<TMessage>(EventProducerCollection sourceEvents)
    {
        if (_destinationMailbox is not  null)
        {
            return ForwardFrom<TMessage>(sourceEvents, _destinationMailbox);
        }

        return null;
    }

    public void ConnectForwardFrom<TMessage>(EventProducerCollection sourceEvents, EventHandler<TMessage> eventHandler)
    {
        ForwardFrom<TMessage>(sourceEvents);
        Connect(eventHandler);
    }
}
