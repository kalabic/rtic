namespace LibRTIC.BasicDevices.RTIC;

public interface ISessionEventProcessor
{
    void ProcessSessionEvent(RTISessionEventId sessionEvent, string? message);
}

public class RTISessionEventProxy : IRTSessionEvents
{
    private ISessionEventProcessor _sep;

    public RTISessionEventProxy(ISessionEventProcessor sep)
    {
        _sep = sep;
    }

    public void ConnectingStarted(string? message = null)
    {
        _sep.ProcessSessionEvent(RTISessionEventId.ConnectingStarted, null);
    }

    public void ConnectingFailed(string? message = null)
    {
        _sep.ProcessSessionEvent(RTISessionEventId.ConnectingFailed, message);
    }

    public void SessionStarted(string? message = null)
    {
        _sep.ProcessSessionEvent(RTISessionEventId.SessionStarted, message);
    }

    public void SessionFinished(string? message = null)
    {
        _sep.ProcessSessionEvent(RTISessionEventId.SessionFinished, message);
    }

    public void ItemStarted(string? message = null)
    {
#if DEBUG_VERBOSE
        _sep.ProcessSessionEvent(RTISessionEventId.ItemStarted, message);
#else
        _sep.ProcessSessionEvent(RTISessionEventId.ItemStarted, null);
#endif
    }

    public void ItemFinished(string? message = null)
    {
        _sep.ProcessSessionEvent(RTISessionEventId.ItemFinished, null);
    }
}
