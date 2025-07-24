namespace LibRTIC.MiniTaskLib.Base;

public abstract class EventForwarderBase : IDisposable
{
#if DEBUG_UNDISPOSED
    public static int UNDISPOSED_COUNT = 0;

    public static int INSTANCE_COUNT = 0;

    private bool _disposed = false;
#endif

#if DEBUG_UNDISPOSED
    public EventForwarderBase()
    {
        Interlocked.Increment(ref UNDISPOSED_COUNT);
        Interlocked.Increment(ref INSTANCE_COUNT);
    }
#endif

#if DEBUG_UNDISPOSED
    ~EventForwarderBase()
    {
        Dispose(false);
        Interlocked.Decrement(ref INSTANCE_COUNT);
    }
#endif

    public void Dispose()
    {
        Dispose(true);
    }

    protected void Dispose(bool disposing)
    {
#if DEBUG_UNDISPOSED
        if (!_disposed && !disposing)
        {
            throw new InvalidOperationException("Not disposed properly.");
        }
        if (!_disposed)
        {
            _disposed = true;
            Interlocked.Decrement(ref UNDISPOSED_COUNT);
        }
#endif

        // Release managed resources.
        if (disposing)
        {
            Disconnect();
        }
        // Release unmanaged resources.
    }

    protected abstract void Disconnect();
}
