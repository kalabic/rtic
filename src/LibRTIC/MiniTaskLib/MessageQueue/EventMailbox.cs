using DotBase.Core;
using DotBase.Log;
using LibRTIC.MiniTaskLib.Base;
using LibRTIC.MiniTaskLib.Events;
using LibRTIC.MiniTaskLib.Model;
using System.Threading.Channels;

namespace LibRTIC.MiniTaskLib.MessageQueue;

public class EventMailbox : DisposableBase, IEventMailboxWriter
{
    public bool IsComplete { get { return IsWriterComplete; } }

    public bool IsWriterComplete { get { return _writerComplete; } }

    public EventQueue Events { get { return _forwardedEvents; } }


    protected EventQueue _forwardedEvents;

    protected EventProducerCollection _events;

    protected InfoLog _info;

    private readonly object _eventLock = new object();

    private readonly object _writerLock = new object();

    private readonly Channel<Action> _mailbox = Channel.CreateUnbounded<Action>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    private bool _writerComplete = false;

    private string _label = "";

    private TaskWithEvents? _queueTaskAwaiter = null;

    private Scheduler _scheduler = new();

    public EventMailbox(InfoLog info)
    {
        _info = info;
        _forwardedEvents = new("EventMailbox Forwarded Events", this);
        _events = new("EventMailbox Events");

        EnableInvokeFor<TaskExceptionOccured>();
        EnableInvokeFor<EventMailboxStarted>();
        EnableInvokeFor<EventMailboxFinished>();

        ForwardEventTo<TaskExceptionOccured>(_forwardedEvents);
        _forwardedEvents.EnableInvokeFor<EventMailboxStarted>();
        _forwardedEvents.EnableInvokeFor<EventMailboxFinished>();
    }

    protected void SetLabel(string label)
    {
        _label = label;
    }

    override protected void Dispose(bool disposing)
    {
        // Release managed resources.
        if (disposing)
        {
            TryCompleteWriter();
            _forwardedEvents.Dispose();
            _events.Dispose();
            _queueTaskAwaiter?.Dispose();
            _scheduler.Dispose();
        }

        // Release unmanaged resources.
        base.Dispose(disposing);
    }

    virtual public TaskWithEvents? GetAwaiter()
    {
        return _queueTaskAwaiter;
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
        try
        {
            lock (_eventLock)
            {
                if (!_events.IsComplete)
                {
                    _events.Invoke(message);
                }
            }
        }
        catch (Exception ex)
        {
            _info.Warning("Exception while invoking mailbox event handlers.", ex);
        }
    }

    protected void NotifyExceptionOccurred(Exception ex)
    {
        _info.Error("Event mailbox failed.", ex);
        InvokeEvent(new TaskExceptionOccured(ex));
    }

    public void DelayedAction(Action action, int delayMs)
    {
        _scheduler.Execute(() => Post(action), delayMs);
    }

    public void RepeatAction(Action action, int delayMs)
    {
        _scheduler.Execute(() => Post(action), delayMs, true);
    }

    public void CloseMailbox()
    {
        PostFinal(NotifyMailboxFinished);
    }

    public virtual void Run()
    {
        Post(NotifyMailboxStarted);
        Run(CancellationToken.None);
    }

    public void Run(CancellationToken cancellation)
    {
        while (WaitToRead(cancellation))
        {
            while (_mailbox.Reader.TryRead(out Action? action))
            {
                ProcessMailboxAction(action);
            }
        }
    }

    public virtual TaskWithEvents RunAsync()
    {
        Post(NotifyMailboxStarted);
        return StartTaskFunctionAsync();
    }

    private TaskWithEvents StartTaskFunctionAsync()
    {
        var queueTask = TaskFunctionAsync();
        _queueTaskAwaiter = ActionTask.RunAction(_info, "Awaiter for " + _label,
            (actionCancellation) => queueTask.Wait(actionCancellation));
        return _queueTaskAwaiter;
    }

    private async Task TaskFunctionAsync()
    {
        await foreach (var action in _mailbox.Reader.ReadAllAsync())
        {
            ProcessMailboxAction(action);
        }
    }

    private bool WaitToRead(CancellationToken cancellation)
    {
        if (_writerComplete || cancellation.IsCancellationRequested)
        {
            return false;
        }

        try
        {
            return _mailbox.Reader.WaitToReadAsync(cancellation).AsTask().GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private void ProcessMailboxAction(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            NotifyExceptionOccurred(ex);
        }
    }

    private void NotifyMailboxStarted()
    {
        _events.Invoke(new EventMailboxStarted());
        _forwardedEvents.Invoke(new EventMailboxStarted());
    }

    private void NotifyMailboxFinished()
    {
        _events.Invoke(new EventMailboxFinished());
        _forwardedEvents.Invoke(new EventMailboxFinished());
    }

    public bool Post(Action action)
    {
        lock (_writerLock)
        {
            return !_writerComplete && _mailbox.Writer.TryWrite(action);
        }
    }

    public bool PostFinal(Action action)
    {
        lock (_writerLock)
        {
            if (_writerComplete)
            {
                return false;
            }

            bool result = _mailbox.Writer.TryWrite(action);
            _writerComplete = _mailbox.Writer.TryComplete();
            return result;
        }
    }

    private bool TryCompleteWriter()
    {
        lock (_writerLock)
        {
            if (!_writerComplete)
            {
                _writerComplete = _mailbox.Writer.TryComplete();
            }
        }
        return _writerComplete;
    }
}
