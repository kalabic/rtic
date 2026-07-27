using DotBase.Log;

namespace LibRTIC.MiniTaskLib.Queues;

public class ActionQueueTask : TaskWithEvents, IActionQueueWriter
{
    public bool IsComplete { get { return IsWriterComplete; } }

    public bool IsWriterComplete { get { return _queue.IsWriterComplete; } }

    public ActionQueue Queue { get { return _queue; } }


    private readonly ActionQueue _queue;

    public ActionQueueTask(InfoLog info)
        : base(info)
    {
        _queue = new ActionQueue(info);
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

    public void CompleteAdding()
    {
        _queue.CompleteAdding();
    }

    public bool Post(Action action)
    {
        return _queue.Post(action);
    }

    public bool PostAndComplete(Action action)
    {
        return _queue.PostAndComplete(action);
    }

    protected override void TaskFunction(CancellationToken cancellation)
    {
        _queue.Run(cancellation);
    }
}
