using DotBase.Core;
using LibRTIC.MiniTaskLib.Model;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;

namespace LibRTIC.MiniTaskLib.Base;

/// <summary>
/// TODO - WIP - This should probably be applied here: https://github.com/Open-NET-Libraries/Open.ChannelExtensions
/// </summary>
/// <typeparam name="TMessage"></typeparam>
public class ChannelContainer<TMessage> : DisposableBase, IQueueWriter<TMessage>
{
    public int Count { get { return _channel.Reader.Count; } }

    public bool IsWriterComplete { get { return _channelIsComplete; } }


    private object _channelLock = new object();

    //  new UnboundedChannelOptions { SingleReader = true });
    private Channel<TMessage> _channel = Channel.CreateUnbounded<TMessage>();

    private bool _channelIsComplete = false;


    protected override void Dispose(bool disposing)
    {
        // Release managed resources.
        if (disposing)
        {
            TryCompleteWriter();
        }
        // Release unmanaged resources.
        base.Dispose(disposing);
    }

    /// <summary>
    /// WTF is this - WIP
    /// </summary>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    public bool WaitToRead(CancellationToken cancellation)
    {
        var reader = _channel.Reader;
        lock(_channelLock)
        {
            if (reader.Count > 0)
            {
                return true;
            }

            if (_channelIsComplete || cancellation.IsCancellationRequested)
            {
                return false;
            }
        }

        ValueTask<bool> valueTask = reader.WaitToReadAsync(cancellation);
        Task<bool> resultTask = valueTask.AsTask();
        resultTask.Wait(cancellation);
        return (reader.Count > 0);
    }

    public async Task<bool> WaitToReadAsync()
    {
        var reader = _channel.Reader;
        lock (_channelLock)
        {
            if (reader.Count > 0)
            {
                return true;
            }

            if (_channelIsComplete)
            {
                return false;
            }
        }

        ValueTask<bool> valueTask = reader.WaitToReadAsync();
        Task<bool> resultTask = valueTask.AsTask();
        await resultTask;
        return (reader.Count > 0);
    }

    public bool TryRead([MaybeNullWhen(false)] out TMessage readMessage)
    {
        if (_channel.Reader.TryRead(out TMessage? message))
        {
            readMessage = message;
            return true;
        }
        readMessage = default;
        return false;
    }

    /// <summary>
    /// Channel lock (<see cref="_channelLock">) needs to be aquired before invoking this.
    /// </summary>
    /// <returns></returns>
    public bool TryCompleteWriter()
    {
        lock (_channelLock)
        {
            if (!_channelIsComplete)
            {
                _channelIsComplete = _channel.Writer.TryComplete();
            }
        }
        return _channelIsComplete;
    }

    public void TryWrite(object? sender, TMessage message)
    {
        lock (_channelLock)
        {
            if (!_channelIsComplete)
            {
                _channel.Writer.TryWrite(message);
            }
        }
    }

    public bool Write(TMessage message)
    {
        bool result = false;
        lock (_channelLock)
        {
            if (!_channelIsComplete)
            {
                result = _channel.Writer.TryWrite(message);
            }
        }
        return result;
    }

    public bool WriteFinal(TMessage message)
    {
        bool result = false;
        lock (_channelLock)
        {
            if (!_channelIsComplete)
            {
                result = _channel.Writer.TryWrite(message);
                _channelIsComplete = _channel.Writer.TryComplete();
            }
        }
        return result;
    }
}
