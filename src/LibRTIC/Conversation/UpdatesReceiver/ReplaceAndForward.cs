using DotBase.Event;
using LibRTIC.MiniTaskLib;

namespace LibRTIC.Conversation.UpdatesReceiver;

public class ReplaceAndForward<TMessage, TArgument> where TMessage : new()
{
    private EventContainer<TMessage>? _event;

    public ReplaceAndForward(EventProducerCollection sourceEvents, EventQueue eventQueue)
    {
        _event = eventQueue.ForwardFrom<TMessage>(sourceEvents);
    }

    public void ProcessNew()
    {
        _event?.Invoke(null, new());
    }
}
