using LibRTIC.BasicDevices;
using LibRTIC.Conversation.Shell;
using LibRTIC.MiniTaskLib;

namespace LibRTIC.Conversation.Devices;

public interface IConversationDevices : IDisposable
{
    public void ConnectingStarted();

    public ExStream GetAudioInput();

    public void ConnectReceiverEvents(EventCollection receiverEvents);

    public void ConnectSessionEvents(EventCollection sessionEvents);

    /// <summary>
    /// Collection of playback related events.
    /// <list type = "bullet">
    ///   <item><see cref="PlaybackFinishedUpdate"></item>
    ///   <item><see cref="PlaybackPositionReachedUpdate"></item>
    /// </list>
    /// </summary>
    public EventCollection? GetAudioEventCollection();

    public bool ClearPlayback(ItemAttributes item);

    public long CancelStopDisposeAll();

    public void EnqueueForPlayback(ItemAttributes item, BinaryData audioData);
}


/// <summary>
/// Part of <see cref="EventCollection"/> returned by <see cref="IConversationDevices.GetAudioEventCollection()"/>
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
/// Part of <see cref="EventCollection"/> returned by <see cref="IConversationDevices.GetAudioEventCollection()"/>
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
