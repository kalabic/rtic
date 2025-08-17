using OpenAI.Realtime;
using LibRTIC.Config;
using LibRTIC.Conversation.Devices;
using LibRTIC.MiniTaskLib;
using LibRTIC.MiniTaskLib.Model;
using System.Diagnostics;

namespace LibRTIC.Conversation.Shell;

#pragma warning disable OPENAI002

/// <summary>
/// WIP
/// </summary>
public abstract class ConversationShell : IDisposable
{
    protected Info _info;

    protected readonly RTIConversation _updatesReceiverTask;

    /// <summary>
    /// Temporary local storage for items from conversation stream. WIP.
    /// </summary>
    private readonly Dictionary<string, ConversationStreamItem> _streamItemMap = new Dictionary<string, ConversationStreamItem>();

    protected readonly IConversationDevices _devices;

    private int _nextLocalItemId = 1;

    protected ConversationShell(Info info,
                                IConversationDevices devices,
                                RealtimeClient client,
                                CancellationToken cancellation)
    {
        this._info = info;
        this._devices = devices;
        _updatesReceiverTask = RTIConversationTask.Create(info, cancellation);
        _updatesReceiverTask.ConfigureWith(client, _devices.GetAudioOutput());

        ConnectDeviceEventHandlers();
        ConnectConversationUpdateHandlers();
    }

    protected ConversationShell(Info info,
                                IConversationDevices devices,
                                ConversationOptions options,
                                CancellationToken cancellation)
    {
        this._info = info;
        this._devices = devices;
        _updatesReceiverTask = RTIConversationTask.Create(info, cancellation);
        _updatesReceiverTask.ConfigureWith(options, _devices.GetAudioOutput());

        ConnectDeviceEventHandlers();
        ConnectConversationUpdateHandlers();
    }

    virtual public void Dispose()
    {
        _streamItemMap.Clear();
        _updatesReceiverTask.Dispose();
        _devices.Dispose();
    }

    /// <summary>
    /// Initiates end of conversation session and returns immediatelly. Should be used only when receiver is running
    /// in asynchronous mode. Shutdown always begings with stopping audio input tasks. In fact, if they are completed 
    /// or broken for any reason that alone should trigger end of session and complete shutdown by itself.
    /// </summary>
    public void Cancel()
    {
        _updatesReceiverTask.Cancel();
    }

    private int getNextLocalItemId()
    {
        int id = _nextLocalItemId++;
        return id;
    }

    public void ReceiveUpdates()
    {
        _devices.ConnectingStarted();
        _updatesReceiverTask.Run();
    }

    public void ReceiveUpdatesAsync()
    {
        _devices.ConnectingStarted();
        _updatesReceiverTask.RunAsync();
    }

    public TaskWithEvents? GetAwaiter()
    {
        return _updatesReceiverTask.GetAwaiter();
    }

    protected abstract void ConnectDeviceEventHandlers();

    private void ConnectConversationUpdateHandlers()
    {
        var conversationEvents = _updatesReceiverTask.ConversationEvents;
        conversationEvents.Connect<ConversationInputSpeechStarted>(false, HandleEvent );
        conversationEvents.Connect<ConversationInputSpeechFinished>(false, HandleEvent );
        conversationEvents.Connect<ConversationItemStreamingStarted>(false, HandleEvent );
        conversationEvents.Connect<ConversationItemStreamingFinished>(false, HandleEvent );
        conversationEvents.Connect<ConversationItemStreamingPartDelta>(false, HandleEvent );
    }

    protected void HandleEvent(object? sender, PlaybackPositionReachedUpdate update)
    {
        if (_streamItemMap.ContainsKey(update.ItemId))
        {
            _streamItemMap.Remove(update.ItemId);
        }
        _devices.ClearPlayback(update.ItemAttrib);
    }

    protected void HandleEvent(object? sender, ConversationInputSpeechStarted update) { }

    protected void HandleEvent(object? sender, ConversationInputSpeechFinished update) { }

    protected void HandleEvent(object? sender, ConversationItemStreamingStarted update)
    {
        ConversationStreamItem streamItem = new ConversationStreamItem(update.ItemId, getNextLocalItemId(), update.FunctionName);
        _streamItemMap.Add(streamItem.Attrib.ItemId, streamItem);
    }

    protected void HandleEvent(object? sender, ConversationItemStreamingFinished update)
    {
        if (_streamItemMap.ContainsKey(update.ItemId))
        {
            ConversationStreamItem item = _streamItemMap[update.ItemId];
            _streamItemMap.Remove(update.ItemId);
        }
    }

    protected void HandleEvent(object? sender, ConversationItemStreamingPartDelta update)
    {
        if (_streamItemMap.ContainsKey(update.ItemId))
        {
            var item = _streamItemMap[update.ItemId];
            if (update.Audio is not null)
            {
                _devices.EnqueueForPlayback(item.Attrib, update.Audio);
            }
        }
    }

    /// <summary>
    /// WIP
    /// </summary>
    /// <param name="timeoutMs"></param>
    /// <returns></returns>
    public long FinishSession(int timeoutMs = -1)
    {
        var stopwatch = new Stopwatch();
        stopwatch.Start();

        // Receiver has its own timeout for cancelling, so not really needed here.
        _updatesReceiverTask.Cancel();
        var awaiter = _updatesReceiverTask.GetAwaiter();
        if (awaiter is not null && !awaiter.IsCompleted)
        {
            awaiter.Wait(timeoutMs);
        }
        stopwatch.Stop();

        // Devices too have a cancel timeout.
        long finishDevicesMs = _devices.CancelStopDisposeAll();

        // Maybe of interest, so return total elapsed cancelling time.
        return stopwatch.ElapsedMilliseconds + finishDevicesMs;
    }
}
