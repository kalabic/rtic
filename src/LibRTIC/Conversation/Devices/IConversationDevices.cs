using AudioFormatLib;
using AudioFormatLib.IO;
using DotBase.Event;
using LibRTIC.Conversation.Shell;

namespace LibRTIC.Conversation.Devices;

/// <summary>
/// Device surface for a conversation shell.
/// Implementations:
/// <list type="bullet">
/// <item><b>Local</b> (single-session): owns hardware capture/playback and optional
/// a hardware playback monitor — see LibRTIC_Win <c>LocalConversationDevices</c>.</item>
/// <item><b>Proxy</b> (multi-session): uses host virtual mic/spk streams; no hardware ownership —
/// see <see cref="ConversationDevicesProxy"/>.</item>
/// </list>
/// </summary>
public interface IConversationDevices : IDisposable
{
    public void ConnectingStarted();

    /// <summary>Gets the mono S16 microphone samples supplied to the Realtime session.</summary>
    public IAudioOutputs GetAudioOutput();

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

    public void EnqueueForPlayback(ItemAttributes item, in AudioPacket audio);
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
