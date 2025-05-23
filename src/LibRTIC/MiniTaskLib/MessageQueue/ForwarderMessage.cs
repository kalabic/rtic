using LibRTIC.MiniTaskLib.Base;

namespace LibRTIC.MiniTaskLib.MessageQueue;

public class ForwarderMessage<TEventArgs> : IProcessMessage
{
    public IInvokeEvent<TEventArgs> _eventProxy;

    public TEventArgs _args;

    public ForwarderMessage(EventForwarder<TEventArgs> eventProxy, TEventArgs args)
    {
        this._eventProxy = eventProxy;
        this._args = args;
    }

    public override void ProcessMessage()
    {
        _eventProxy.Invoke(_args);
    }
}
