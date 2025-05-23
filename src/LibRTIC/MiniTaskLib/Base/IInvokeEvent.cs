namespace LibRTIC.MiniTaskLib.Base;

public abstract class IInvokeEvent<TEventArgs> : EventForwarderBase
{
    abstract public void Invoke(TEventArgs ev);
}
