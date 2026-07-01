using AudioFormatLib.IO;
using LibRTIC.Conversation.Shell;
using LibRTIC.MiniTaskLib;

namespace LibRTIC.Conversation.Devices;

public interface IConversationDevices : IDisposable
{
    public void ConnectingStarted();

    public IAudioBufferOutput GetAudioOutput();

    public void ConnectReceiverEvents(EventProducerCollection receiverEvents);

    public void ConnectSessionEvents(EventProducerCollection sessionEvents);

    /// <summary>
    /// Playback related events.
    /// <list type = "bullet">
    ///   <item><see cref="PlaybackFinishedUpdate"></item>
    ///   <item><see cref="PlaybackPositionReachedUpdate"></item>
    /// </list>
    /// </summary>
    public EventProducerCollection? GetPlaybackEvents();

    public bool ClearPlayback(ItemAttributes item);

    public long CancelStopDisposeAll();

    public void EnqueueForPlayback(ItemAttributes item, BinaryData audioData);
}


/// <summary>
/// Part of <see cref="EventProducerCollection"/> returned by <see cref="IConversationDevices.GetPlaybackEvents()"/>
/// </summary>
public class PlaybackFinishedUpdate
{
    public const PlaybackFinishedUpdate? Default = null;

    private ItemAttributes enqueuedItem;

    public PlaybackFinishedUpdate(ItemAttributes enqueuedItem)
    {
        this.enqueuedItem = enqueuedItem;
    }
}

/// <summary>
/// Part of <see cref="EventProducerCollection"/> returned by <see cref="IConversationDevices.GetPlaybackEvents()"/>
/// </summary>
public class PlaybackPositionReachedUpdate
{
    public const PlaybackPositionReachedUpdate? Default = null;

    public string ItemId { get { return _itemAttrib.ItemId; } }

    public ItemAttributes ItemAttrib { get { return _itemAttrib; } }

    private ItemAttributes _itemAttrib;

    public PlaybackPositionReachedUpdate(ItemAttributes itemAttrib)
    {
        this._itemAttrib = new ItemAttributes(itemAttrib);
    }
}
