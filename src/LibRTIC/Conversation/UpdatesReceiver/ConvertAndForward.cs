using LibRTIC.MiniTaskLib;

namespace LibRTIC.Conversation.UpdatesReceiver;

public class ConvertAndForward<TMessage, TArgument>
{
    private EventContainer<TMessage>? _event;

    private Func<TArgument, TMessage> _function;

    public ConvertAndForward(EventCollection eventCollection, EventQueue eventQueue, Func<TArgument, TMessage> function)
    {
        _event = eventQueue.ForwardFrom<TMessage>(eventCollection);
        _function = function;
    }

    public void Convert(TArgument argument)
    {
        var result = _function(argument);
        _event?.Invoke(null, result);
    }
}
