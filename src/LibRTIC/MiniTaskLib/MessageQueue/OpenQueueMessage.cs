using LibRTIC.MiniTaskLib.Base;
using LibRTIC.MiniTaskLib.Events;

namespace LibRTIC.MiniTaskLib.MessageQueue;

public class OpenQueueMessage : IProcessMessage
{
    public EventCollection _events;

    public EventCollection _forwardedEvents;

    public OpenQueueMessage(EventCollection events, EventCollection forwardedEvents)
    {
        _events = events;
        _forwardedEvents = forwardedEvents;
    }

    public override void ProcessMessage()
    {
        _events.Invoke(new MessageQueueStarted());
        _forwardedEvents.Invoke(new MessageQueueStarted());
    }
}
