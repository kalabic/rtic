using DotBase.Core;
using DotBase.Event;
using DotBase.Log;
using LibRTIC.MiniTaskLib.Base;
using LibRTIC.MiniTaskLib.Events;
using System.Threading.Channels;

namespace LibRTIC.MiniTaskLib.Queues;

public class ActionQueue : DisposableBase, IActionQueue
{
    public bool IsComplete { get { return IsWriterComplete; } }

    public bool IsWriterComplete { get { return _writerComplete; } }

    public EventQueue Events { get { return _dispatchedEvents; } }


    protected EventQueue _dispatchedEvents;

    protected EventProducerCollection _sourceEvents;

    protected InfoLog _info;

    private readonly object _eventLock = new object();

    private readonly object _writerLock = new object();

    private readonly Channel<Action> _queue = Channel.CreateUnbounded<Action>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    private bool _writerComplete = false;

    private string _label = "";

    private TaskWithEvents? _queueTaskAwaiter = null;

    private readonly Scheduler _scheduler;

    public ActionQueue(InfoLog info)
        : this(info, TimeProvider.System)
    { }

    internal ActionQueue(InfoLog info, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        _info = info;
        _dispatchedEvents = new("Action Queue Dispatched Events", this);
        _sourceEvents = new("Action Queue Source Events");
        _scheduler = new(timeProvider, NotifyExceptionOccurred);

        EnableInvokeFor<TaskExceptionOccured>();
        EnableInvokeFor<ActionQueueStarted>();
        EnableInvokeFor<ActionQueueDrained>();

        ForwardEventTo<TaskExceptionOccured>(_dispatchedEvents);
        _dispatchedEvents.EnableInvokeFor<ActionQueueStarted>();
        _dispatchedEvents.EnableInvokeFor<ActionQueueDrained>();
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
            _scheduler.Dispose();
            _dispatchedEvents.Dispose();
            _sourceEvents.Dispose();
            _queueTaskAwaiter?.Dispose();
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
        _sourceEvents.EnableInvokeFor<TMessage>();
    }

    protected void ForwardEventTo<TMessage>(EventQueue forwarder)
    {
        forwarder.ForwardFrom<TMessage>(_sourceEvents);
    }

    protected void InvokeEvent<TMessage>(TMessage message)
    {
        try
        {
            lock (_eventLock)
            {
                if (!_sourceEvents.IsComplete)
                {
                    _sourceEvents.Invoke(message);
                }
            }
        }
        catch (Exception ex)
        {
            if (string.IsNullOrWhiteSpace(_label))
            {
                _info.Warning("Exception while invoking action queue event handlers.", ex);
            }
            else
            {
                _info.Warning($"Exception while invoking '{_label}' handlers.", ex);
            }
        }
    }

    protected void NotifyExceptionOccurred(Exception ex)
    {
        if (string.IsNullOrWhiteSpace(_label))
        {
            _info.Error("Action queue failed.", ex);
        }
        else
        {
            _info.Error($"'{_label}' failed.", ex);
        }
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

    public void CompleteAdding()
    {
        PostAndComplete(NotifyQueueDrained);
    }

    public virtual void Run()
    {
        Post(NotifyQueueStarted);
        Run(CancellationToken.None);
    }

    public void Run(CancellationToken cancellation)
    {
        while (WaitToRead(cancellation))
        {
            while (_queue.Reader.TryRead(out Action? action))
            {
                ExecuteAction(action);
            }
        }
    }

    public virtual TaskWithEvents RunAsync()
    {
        Post(NotifyQueueStarted);
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
        await foreach (var action in _queue.Reader.ReadAllAsync())
        {
            ExecuteAction(action);
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
            return _queue.Reader.WaitToReadAsync(cancellation).AsTask().GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private void ExecuteAction(Action action)
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

    private void NotifyQueueStarted()
    {
        _sourceEvents.Invoke(new ActionQueueStarted());
        _dispatchedEvents.Invoke(new ActionQueueStarted());
    }

    private void NotifyQueueDrained()
    {
        _sourceEvents.Invoke(new ActionQueueDrained());
        _dispatchedEvents.Invoke(new ActionQueueDrained());
    }

    public bool Post(Action action)
    {
        lock (_writerLock)
        {
            return !_writerComplete && _queue.Writer.TryWrite(action);
        }
    }

    public bool PostAndComplete(Action action)
    {
        lock (_writerLock)
        {
            if (_writerComplete)
            {
                return false;
            }

            bool result = _queue.Writer.TryWrite(action);
            _writerComplete = _queue.Writer.TryComplete();
            return result;
        }
    }

    private bool TryCompleteWriter()
    {
        lock (_writerLock)
        {
            if (!_writerComplete)
            {
                _writerComplete = _queue.Writer.TryComplete();
            }
        }
        return _writerComplete;
    }
}
