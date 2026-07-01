namespace LibRTIC.MiniTaskLib.Model;

public interface IEventMailboxWriter
{
    public bool IsWriterComplete { get; }

    public bool Post(Action action);

    public bool PostFinal(Action action);
}
