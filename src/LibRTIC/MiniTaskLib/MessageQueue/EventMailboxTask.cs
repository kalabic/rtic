using DotBase.Log;
using LibRTIC.MiniTaskLib.Model;

namespace LibRTIC.MiniTaskLib.MessageQueue;

public class EventMailboxTask : TaskWithEvents, IEventMailboxWriter
{
    public bool IsComplete { get { return IsWriterComplete; } }

    public bool IsWriterComplete { get { return _mailbox.IsWriterComplete; } }

    public EventMailbox Mailbox { get { return _mailbox; } }


    private EventMailbox _mailbox;

    public EventMailboxTask(InfoLog info)
        : base(info)
    {
        _mailbox = new EventMailbox(info);
    }

    override protected void Dispose(bool disposing)
    {
        // Release managed resources.
        if (disposing)
        {
            _mailbox.Dispose();
        }

        // Release unmanaged resources.
        base.Dispose(disposing);
    }

    public void CloseMailbox()
    {
        _mailbox.CloseMailbox();
    }

    public bool Post(Action action)
    {
        return _mailbox.Post(action);
    }

    public bool PostFinal(Action action)
    {
        return _mailbox.PostFinal(action);
    }

    protected override void TaskFunction(CancellationToken cancellation)
    {
        _mailbox.Run(cancellation);
    }
}
