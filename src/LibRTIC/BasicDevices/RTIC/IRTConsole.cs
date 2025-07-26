namespace LibRTIC.BasicDevices.RTIC;

public interface IRTConsole : IRTWriter
{
    public abstract IRTSessionEvents Event { get; }
}
