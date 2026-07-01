using DotBase.Event;
using DotBase.Log;
using LibRTIC.MiniTaskLib.Model;

namespace LibRTIC.MiniTaskLib;

public class EventQueue : EventCollection
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

    public EventContainer<TMessage>? ForwardFrom<TMessage>(EventCollection otherCollection)
    {
        if (_destinationMailbox is not  null)
        {
            return ForwardFrom<TMessage>(otherCollection, _destinationMailbox);
        }

        return null;
    }

    public void ConnectForwardFrom<TMessage>(EventCollection otherCollection, EventHandler<TMessage> eventHandler)
    {
        ForwardFrom<TMessage>(otherCollection);
        Connect(eventHandler);
    }
}
