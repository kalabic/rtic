using LibRTIC.MiniTaskLib.Base;
using LibRTIC.MiniTaskLib.Events;
using LibRTIC.MiniTaskLib.Model;

namespace LibRTIC.MiniTaskLib.MessageQueue;

public class ForwardedEventQueue : MessageQueueFunction<IProcessMessage>
{
    public EventQueue Events { get {  return _forwardedEvents; } }


    protected EventQueue _forwardedEvents;

    private object _lock = new object();

    protected EventCollection _events;

    public ForwardedEventQueue(Info info)
        : this(info, new CancellationTokenSource())
    { }

    private ForwardedEventQueue(Info info, CancellationTokenSource cancellationSource)
        : base(info)
    {
        this._forwardedEvents = new(_info, "ForwardedEventQueue Forwarded Events", _channel);
        this._events = new(info, "ForwardedEventQueue Events");

        EnableInvokeFor<TaskExceptionOccured>();
        EnableInvokeFor<MessageQueueStarted>();
        EnableInvokeFor<MessageQueueFinished>();

        ForwardEventTo<TaskExceptionOccured>(_forwardedEvents);
        _forwardedEvents.EnableInvokeFor<MessageQueueStarted>();
        _forwardedEvents.EnableInvokeFor<MessageQueueFinished>();
    }

    override protected void Dispose(bool disposing)
    {
        // Release managed resources.
        if (disposing)
        {
            _forwardedEvents.Dispose();
            _events.Dispose();
        }

        // Release unmanaged resources.
        base.Dispose(disposing);
    }

    protected void EnableInvokeFor<TMessage>()
    {
        _events.EnableInvokeFor<TMessage>();
    }

    protected void ForwardEventTo<TMessage>(EventQueue forwarder)
    {
        forwarder.ForwardFrom<TMessage>(_events);
    }

    protected void InvokeEvent<TMessage>(TMessage message)
    {
        lock (_lock)
        {
            if (!_events.IsComplete)
            {
                _events.Invoke(message);
            }
        }
    }

    protected void NotifyExceptionOccurred(Exception ex)
    {
        _info.ExceptionOccured(ex);
        InvokeEvent(new TaskExceptionOccured(ex));
    }

    public void DelayedAction(Action action, int delayMs)
    {
        WriteDelayed(new ScheduledAction(action), delayMs);
    }

    public void RepeatAction(Action action, int delayMs)
    {
        WriteRepeatedly(new ScheduledAction(action), delayMs);
    }

    public void CloseMessageQueue()
    {
        WriteFinal(new CloseQueueMessage(_events, _forwardedEvents));
    }

    public override void Run()
    {
        base.Run(new OpenQueueMessage(_events, _forwardedEvents));
    }

    public override TaskWithEvents RunAsync()
    {
        return base.RunAsync(new OpenQueueMessage(_events, _forwardedEvents));
    }

    protected override void ProcessMessage(IProcessMessage message)
    {
        message.ProcessMessage();
    }
}
