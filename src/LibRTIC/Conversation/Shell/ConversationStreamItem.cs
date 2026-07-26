namespace LibRTIC.Conversation.Shell;

/// <summary>
/// WIP
/// </summary>
public class ConversationStreamItem
{
    public readonly ConversationOutputKey Key;

    public readonly ItemAttributes Attrib;

    public readonly string FunctionName;

    public string FunctionAttributes;

    public string ItemId { get { return Attrib.ItemId; } }

    public int LocalItemId { get { return Attrib.LocalId; } }

    public ConversationStreamItem(
        ConversationOutputKey key,
        int localItemId,
        string functionName)
    {
        this.Key = key;
        this.Attrib = new ItemAttributes(key.ItemId, localItemId);
        this.FunctionName = functionName;
        this.FunctionAttributes = "";
    }
}

public readonly record struct ConversationOutputKey(
    string ResponseId,
    string ItemId,
    int OutputIndex);

public readonly record struct ConversationContentKey(
    string ResponseId,
    string ItemId,
    int OutputIndex,
    int ContentIndex)
{
    public ConversationOutputKey OutputKey =>
        new(ResponseId, ItemId, OutputIndex);
}
