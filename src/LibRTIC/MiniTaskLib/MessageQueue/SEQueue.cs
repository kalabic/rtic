using LibRTIC.BasicDevices;
using LibRTIC.MiniTaskLib.Model;

namespace LibRTIC.MiniTaskLib.MessageQueue;

public class SEQueue : ForwardedEventQueueTask
{
    static public EventQueue Events 
    { 
        get 
        { 
            if (_staticEvents is null)
            {
                _staticQueue = new SEQueue(ConsoleNotification.StatDevOut);
                _staticEvents = _staticQueue.Queue.Events;
            }

            return _staticEvents;
        }
    }

    static protected SEQueue? _staticQueue = null;

    static protected EventQueue? _staticEvents = null;

    protected SEQueue(Info info) 
        : base(info)
    { }
}
