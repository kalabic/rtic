using LibRTIC.MiniTaskLib.Base;
using LibRTIC.MiniTaskLib.Model;

namespace LibRTIC.MiniTaskLib.MessageQueue;

public abstract class MessageQueueFunction<TMessage> : DisposableBase, IQueueWriter<TMessage>
{
    public bool IsWriterComplete { get { return _channel.IsWriterComplete; } }

    public string QueueLabel { get { return _label; } }


    protected ChannelContainer<TMessage> _channel = new();

    protected Info _info;

    private string _label = "";

    private TaskWithEvents? _queueTaskAwaiter = null;

    private Scheduler _scheduler = new();

    public MessageQueueFunction(Info info)
    {
        this._info = info;
    }

    protected void SetLabel(string label)
    {
        this._label = label;
    }

    protected override void Dispose(bool disposing)
    {
        // Release managed resources.
        if (disposing)
        {
            _channel.Dispose();
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

    public virtual void Run()
    {
        Run(CancellationToken.None);
    }

    public void Run(TMessage initialMessage)
    {
        _channel.Write(initialMessage);
        Run(CancellationToken.None);
    }

    public void Run(CancellationToken cancellation)
    {
        while (_channel.WaitToRead(cancellation))
        {
            while (_channel.TryRead(out TMessage? message))
            {
                ProcessMessage(message);
            }
        }
    }

    public TaskWithEvents RunAsync(TMessage initialMessage)
    {
        _channel.Write(initialMessage);
        return StartTaskFunctionAsync();
    }

    public virtual TaskWithEvents RunAsync()
    {
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
        while (await _channel.WaitToReadAsync())
        {
            while (_channel.TryRead(out TMessage? message))
            {
                ProcessMessage(message);
            }
        }
    }

    abstract protected void ProcessMessage(TMessage message);

    public bool Write(TMessage message)
    {
        return _channel.Write(message);
    }

    public void WriteDelayed(TMessage message, int delayMs)
    {
        _scheduler.Execute(() => Write(message), delayMs);
    }

    public void WriteRepeatedly(TMessage message, int periodMs)
    {
        _scheduler.Execute(() => Write(message), periodMs, true);
    }

    public bool WriteFinal(TMessage message)
    {
        return _channel.WriteFinal(message);
    }
}
