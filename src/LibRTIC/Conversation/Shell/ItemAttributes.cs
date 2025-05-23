namespace LibRTIC.Conversation.Shell;

public class ItemAttributes
{
    // 'ItemId' is assigned to the item by the server.
    public string ItemId;

    // 'LocalId' is assigned locally, it is an integer increased by one for every consecutive item received from the server.
    public int LocalId;

    public ItemAttributes()
    {
        ItemId = "";
        LocalId = -1;
    }

    public ItemAttributes(ItemAttributes other)
    {
        ItemId = other.ItemId;
        LocalId = other.LocalId;
    }

    public ItemAttributes(string itemId, int localItemId)
    {
        this.ItemId = itemId;
        this.LocalId = localItemId;
    }

    public void Clear()
    {
        ItemId = "";
        LocalId = -1;
    }

    public void Set(ItemAttributes other)
    {
        ItemId = other.ItemId;
        LocalId = other.LocalId;
    }
}
