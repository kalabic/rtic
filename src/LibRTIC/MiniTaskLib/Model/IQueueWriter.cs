namespace LibRTIC.MiniTaskLib.Model;

public interface IQueueWriter<TMessage>
{
    public bool IsWriterComplete { get; }

    public bool Write(TMessage message);

    public bool WriteFinal(TMessage message);
}
