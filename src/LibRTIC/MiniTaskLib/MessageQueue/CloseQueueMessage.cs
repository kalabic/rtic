using LibRTIC.MiniTaskLib.Base;
using LibRTIC.MiniTaskLib.Events;

namespace LibRTIC.MiniTaskLib.MessageQueue;

public class CloseQueueMessage : IProcessMessage
{
    public EventCollection _events;

    public EventCollection _forwardedEvents;

    public CloseQueueMessage(EventCollection events, EventCollection forwardedEvents)
    {
        _events = events;
        _forwardedEvents = forwardedEvents;
    }

    public override void ProcessMessage()
    {
        _events.Invoke(new MessageQueueFinished());
        _forwardedEvents.Invoke(new MessageQueueFinished());
    }
}
