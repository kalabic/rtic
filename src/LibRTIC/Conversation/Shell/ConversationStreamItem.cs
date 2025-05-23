namespace LibRTIC.Conversation.Shell;

/// <summary>
/// WIP
/// </summary>
public class ConversationStreamItem
{
    public readonly ItemAttributes Attrib;

    public readonly string FunctionName;

    public string FunctionAttributes;

    public string ItemId { get { return Attrib.ItemId; } }

    public int LocalItemId { get { return Attrib.LocalId; } }

    public ConversationStreamItem(string itemId, int localItemId, string functionName)
    {
        this.Attrib = new ItemAttributes(itemId, localItemId);
        this.FunctionName = functionName;
        this.FunctionAttributes = "";
    }
}
