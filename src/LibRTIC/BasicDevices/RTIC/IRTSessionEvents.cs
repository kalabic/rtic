namespace LibRTIC.BasicDevices.RTIC;

/// <summary>
/// This should be used by main program to notify <see cref="RTIConsole"/> about general 
/// state of conversation session.
/// </summary>
public interface IRTSessionEvents
{
    public void ConnectingStarted(string? message = null);

    public void ConnectingFailed(string? message = null);

    public void SessionStarted(string? message = null);

    public void SessionFinished(string? message = null);

    public void ItemStarted(string? message = null);

    public void ItemFinished(string? message = null);
}
