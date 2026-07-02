using DotBase.Event;

namespace LibRTIC.MiniTaskLib;

/// <summary>
/// Event producer collection that forwards events through a dispatcher by default.
/// </summary>
public class DispatchingEventProducerCollection : EventProducerCollection
{
    private IActionDispatcher? _dispatcher = null;

    public DispatchingEventProducerCollection(string label, IActionDispatcher dispatcher)
         : base(label)
    {
        _dispatcher = dispatcher;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _dispatcher = null;
        }
        else
        {
            if (_dispatcher is not null)
            {
                throw new InvalidOperationException("Not disposed properly.");
            }
        }

        base.Dispose(disposing);
    }

    public EventContainer<TMessage>? ForwardFrom<TMessage>(EventProducerCollection sourceEvents)
    {
        if (_dispatcher is not null)
        {
            return ForwardFrom<TMessage>(sourceEvents, _dispatcher);
        }

        return null;
    }

    public void ConnectForwardFrom<TMessage>(EventProducerCollection sourceEvents, EventHandler<TMessage> eventHandler)
    {
        ForwardFrom<TMessage>(sourceEvents);
        Connect(eventHandler);
    }
}
