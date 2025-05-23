using LibRTIC.MiniTaskLib.Base;
using LibRTIC.MiniTaskLib.Model;

namespace LibRTIC.MiniTaskLib.MessageQueue;

public class ForwardedEventQueueTask : TaskWithEvents, IQueueWriter<IProcessMessage>
{
    public bool IsWriterComplete { get { return _queue.IsWriterComplete; } }

    public ForwardedEventQueue Queue { get { return _queue; } }


    private ForwardedEventQueue _queue;

    public ForwardedEventQueueTask(Info info)
        : base(info)
    {
        _queue = new ForwardedEventQueue(info);
    }

    override protected void Dispose(bool disposing)
    {
        // Release managed resources.
        if (disposing)
        {
            _queue.Dispose();
        }

        // Release unmanaged resources.
        base.Dispose(disposing);
    }

    public void CloseMessageQueue()
    {
        _queue.CloseMessageQueue();
    }

    public bool Write(IProcessMessage message)
    {
        return _queue.Write(message);
    }

    public bool WriteFinal(IProcessMessage message)
    {
        return _queue.WriteFinal(message);
    }

    protected override void TaskFunction(CancellationToken cancellation)
    {
        _queue.Run(cancellation);
    }
}
