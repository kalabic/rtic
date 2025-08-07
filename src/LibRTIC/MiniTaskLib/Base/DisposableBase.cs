namespace LibRTIC.MiniTaskLib.Base;

public abstract class DisposableBase : IDisposable
{
    public bool IsDisposed { get { return _disposed; } protected set { _disposed = value; } }

    private bool _disposed = false;

    ~DisposableBase()
    {
        Dispose(false);
    }

    public void Dispose()
    {
        Dispose(true);
    }

    protected virtual void Dispose(bool disposing)
    {
#if DEBUG_UNDISPOSED
        if (!_disposed && !disposing)
        {
            throw new InvalidOperationException("Not disposed properly.");
        }
        if (!_disposed)
        {
            _disposed = true;
        }
#endif
        // Release managed resources: disposing = true
        // Release unmanaged resources: disposing = false
    }
}
