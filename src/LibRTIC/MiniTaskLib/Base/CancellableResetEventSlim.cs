using LibRTIC.MiniTaskLib.Model;

namespace LibRTIC.MiniTaskLib;

public class CancellableResetEventSlim : IDisposable
{
    public bool IsCancellationRequested { get {  return _cancellation.IsCancellationRequested; } }

    private Info _info;

    private CancellationToken _cancellation;

    private ManualResetEventSlim _cancellationEvent = new ManualResetEventSlim();

    private ManualResetEventSlim _event = new ManualResetEventSlim();

    private WaitHandle[] _waitHandles;

    public CancellableResetEventSlim(Info info, CancellationToken cancellation)
    {
        _info = info;
        _cancellation = cancellation;
        _cancellation.Register( () => _cancellationEvent.Set() );
        _waitHandles = [_event.WaitHandle, _cancellationEvent.WaitHandle];
    }

    public void Dispose()
    {
        _cancellationEvent.Dispose();
        _event.Dispose();
    }

    public void Cancel()
    {
        _cancellationEvent.Set();
    }

    public bool Reset()
    {
        bool isActive = !_cancellation.IsCancellationRequested;
        if (isActive)
        {
            _event.Reset();
        }
        else
        {
            if (!_event.IsSet)
            {
                _event.Set();
            }
        }
        return isActive;
    }

    public bool Set()
    {
        bool isActive = !_cancellation.IsCancellationRequested;
        if (isActive)
        {
            _event.Set();
        }
        else
        {
            if (!_event.IsSet)
            {
                _event.Set();
            }
        }
        return isActive;
    }

    public bool Wait()
    {
        bool isActive = !_cancellation.IsCancellationRequested;
        int index = -1;
        if (isActive)
        {
            try
            {
                index = WaitHandle.WaitAny( _waitHandles );
            }
            catch (OperationCanceledException ex)
            {
                _info.ExceptionOccured(ex);
            }
            catch (ObjectDisposedException ex)
            {
                _info.ExceptionOccured(ex);
            }
            catch (InvalidOperationException ex)
            {
                _info.ExceptionOccured(ex);
            }

            isActive = !_cancellation.IsCancellationRequested;
        }

        if (!isActive)
        {
            if (!_event.IsSet)
            {
                _event.Set();
            }
        }

        return isActive && (index == 0);
    }
}
