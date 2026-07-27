using DotBase.Event;

namespace LibRTIC.MiniTaskLib;

/// <summary>
/// LibRTIC compatibility name for a dispatching event producer collection backed by an action queue.
/// </summary>
public class EventQueue : DispatchingEventProducerCollection
{
    public EventQueue(string label, IActionDispatcher dispatcher)
         : base(label, dispatcher)
    { }
}
