using LibRTIC.MiniTaskLib.Base;
using LibRTIC.MiniTaskLib.Model;

namespace LibRTIC.MiniTaskLib;

public class EventQueue : EventCollection
{
    private IQueueWriter<IProcessMessage>? _destinationQueue = null;

    public EventQueue(Info info, string label, IQueueWriter<IProcessMessage> destinationQueue)
         : base(info, label)
    {
        _destinationQueue = destinationQueue;
    }

    protected override void Dispose(bool disposing)
    {
        // Release managed resources.
        if (disposing)
        {
            _destinationQueue = null;
        }
        else
        {
            if (_destinationQueue is not null)
            {
                throw new InvalidOperationException("Not disposed properly.");
            }
        }

        // Release unmanaged resources.
        base.Dispose(disposing);
    }

    public EventContainer<TMessage>? ForwardFrom<TMessage>(EventCollection otherCollection)
    {
        if (_destinationQueue is not  null)
        {
            return ForwardFrom<TMessage>(otherCollection, _destinationQueue);
        }

        return null;
    }

    public void ConnectForwardFrom<TMessage>(EventCollection otherCollection, EventHandler<TMessage> eventHandler)
    {
        ForwardFrom<TMessage>(otherCollection);
        Connect(eventHandler);
    }
}
