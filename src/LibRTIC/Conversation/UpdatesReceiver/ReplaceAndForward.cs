using LibRTIC.MiniTaskLib;

namespace LibRTIC.Conversation.UpdatesReceiver;

public class ReplaceAndForward<TMessage, TArgument> where TMessage : new()
{
    private EventContainer<TMessage>? _event;

    public ReplaceAndForward(EventCollection eventCollection, EventQueue eventQueue)
    {
        _event = eventQueue.ForwardFrom<TMessage>(eventCollection);
    }

    public void ProcessNew()
    {
        _event?.Invoke(null, new());
    }
}
