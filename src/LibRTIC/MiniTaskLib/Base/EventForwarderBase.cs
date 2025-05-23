namespace LibRTIC.MiniTaskLib.Base;

public abstract class EventForwarderBase : IDisposable
{
    public void Dispose()
    {
        Dispose(true);
    }

    protected void Dispose(bool disposing)
    {
        // Release managed resources.
        if (disposing)
        {
            Disconnect();
        }
        // Release unmanaged resources.
    }

    protected abstract void Disconnect();
}
