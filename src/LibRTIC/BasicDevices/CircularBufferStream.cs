using LibRTIC.MiniTaskLib;
using LibRTIC.MiniTaskLib.Model;
using System.Text;

namespace LibRTIC.BasicDevices;

public class CircularBufferStream : ExStream
{
    public bool IsCancellationRequested { get { return _streamEvent.IsCancellationRequested; } }

    public override bool IsClosed { get { return _streamBuffer is null; } }

    protected readonly Info _info;

    private readonly object _lockObject;

    private CancellableResetEventSlim _streamEvent;

    private CircularBuffer? _streamBuffer = null;

    protected long _totalBytesWritten = 0;

    protected int _bufferRequest = 0;

    protected bool _disposed = false;

    public CircularBufferStream(Info info, int bufferSize, CancellationToken cancellation)
    {
        _info = info;
        _lockObject = new object();
        _streamEvent = new CancellableResetEventSlim(info, cancellation);
        _streamBuffer = new CircularBuffer(bufferSize);
    }

    public CircularBufferStream(Info info, int bufferSize)
        : this(info, bufferSize, CancellationToken.None)
    { }

    protected override void Dispose(bool disposing)
    {
        // Release managed resources.
        if (disposing)
        {
            lock (_lockObject)
            {
                _disposed = true;
                if (_streamBuffer is not null)
                {
                    _streamBuffer.Reset();
                    _streamBuffer = null;
                }
                _streamEvent.Dispose();
            }
#if DEBUG_VERBOSE_DISPOSE
            _info.ObjectDisposed(this);
#endif
        }

        // Release unmanaged resources.
        base.Dispose(disposing);
    }

    public override void Cancel()
    {
        _streamEvent.Cancel();
    }

    public override void Close()
    {
        base.Close();
        GC.ReRegisterForFinalize(this); // <---- Important!
        lock(_lockObject)
        {
            if (_streamBuffer is not null)
            {
                _streamBuffer.Reset();
                _streamBuffer = null;
            }
        }
    }

    public void Write(byte[] buffer)
    {
        Write(buffer, 0, buffer.Length);
    }

    public void Write(string text)
    {
        if (!String.IsNullOrEmpty(text))
        {
            Write(Encoding.UTF8.GetBytes(text));
        }
    }

    public override void ClearBuffer()
    {
        lock (_lockObject)
        {
            _streamBuffer?.Reset();
            _totalBytesWritten = 0;

            // Temporary workaround for propagating clear buffer request.
            SetBufferRequest(1);
        }
    }

    public override int GetBufferRequest()
    {
        int req = _bufferRequest;
        _bufferRequest = 0;
        return req;
    }

    public override void SetBufferRequest(int value)
    {
        _bufferRequest = value;
    }

    public int GetBufferedBytes()
    {
        return (_streamBuffer is not null && !IsCancellationRequested) ? _streamBuffer.Count : 0;
    }

    public bool WaitDataAvailable(int minAvailable, int timeoutMs = 0)
    {
        int available = GetBytesAvailable(minAvailable);
        if (available >= minAvailable)
        {
            return true;
        }
        else if (available < 0 || IsCancellationRequested)
        {
            // _streamBuffer is null
            return false;
        }

        try
        {
            while (!IsCancellationRequested)
            {
                if (_streamEvent.Wait())
                {
                    available = GetBytesAvailable(minAvailable);
                    if (available >= minAvailable)
                    {
                        return true;
                    }
                    else if (available < 0)
                    {
                        // _streamBuffer is null
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }
        }
        catch (Exception ex)
        {
            _info.ExceptionOccured(ex);
        }

        return false;
    }

    public override int GetBytesAvailable()
    {
        lock (_lockObject)
        {
            if (!IsCancellationRequested)
            {
                return (_streamBuffer is not null) ? _streamBuffer.Count : -1;
            }
            else
            {
                return 0;
            }
        }
    }

    public override int GetBytesUnused()
    {
        lock (_lockObject)
        {
            if (!IsCancellationRequested)
            {
                return (_streamBuffer is not null) ? _streamBuffer.UnusedCount : -1;
            }
            else
            {
                return 0;
            }
        }
    }

    virtual protected int GetBytesAvailable(int minAvailable)
    {
        lock (_lockObject)
        {
            if (!IsCancellationRequested)
            {
                int available = (_streamBuffer is not null) ? _streamBuffer.Count : -1;
                if (available < minAvailable)
                {
                    _streamEvent.Reset();
                }
                return available;
            }
            else
            {
                return 0;
            }
        }
    }

    public override bool CanRead => (_streamBuffer is not null && !IsCancellationRequested);

    public override bool CanSeek => throw new NotImplementedException();

    public override bool CanWrite => (_streamBuffer is not null && !IsCancellationRequested);

    public override long Length => throw new NotImplementedException();

    public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public override void Flush()
    {
        throw new NotImplementedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        lock (_lockObject)
        {
            if (_streamBuffer is not null && !IsCancellationRequested)
            {
                int bytesRead = _streamBuffer.Read(buffer, offset, count);
                if (_streamBuffer.Count == 0)
                {
                    _streamEvent.Reset();
                }
                return bytesRead;
            }
        }
        return 0;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotImplementedException();
    }

    public override void SetLength(long value)
    {
        throw new NotImplementedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        lock (_lockObject)
        {
            if (_streamBuffer is not null && !IsCancellationRequested)
            {
                _totalBytesWritten += _streamBuffer.Write(buffer, offset, count);
                _streamEvent.Set();
            }
        }
    }

    public override int MovePacket(ExStream other, byte[] buffer)
    {
        int bytesRead = 0;

        lock (_lockObject)
        {
            if (_streamBuffer is not null && 
                !IsCancellationRequested &&
                _streamBuffer.Count >= buffer.Length)
            {
                bytesRead = _streamBuffer.Read(buffer, 0, buffer.Length);
                if (_streamBuffer.Count == 0)
                {
                    _streamEvent.Reset();
                }
            }
        }

        if (bytesRead == buffer.Length)
        {
            other.Write(buffer, 0, buffer.Length);
            return bytesRead;
        }

        return 0;
    }
}
