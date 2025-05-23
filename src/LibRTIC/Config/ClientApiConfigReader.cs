using LibRTIC.MiniTaskLib.Model;
using System.Text.Json.Nodes;

namespace LibRTIC.Config;

public class ClientApiConfigReader : ClientApiConfig
{
    static public ClientApiConfigReader FromFileOrEnvironment(Info info, string path)
    {
        ClientApiConfigReader options = new ClientApiConfigReader(info);
        options.fromFileOrEnvironment(path);
        return options;
    }

    static public ClientApiConfigReader FromFile(Info info, string path)
    {
        ClientApiConfigReader options = new ClientApiConfigReader(info);
        options.fromFileJson(path);
        return options;
    }

    private static ReadOnlySpan<byte> Utf8Bom => new byte[] { 0xEF, 0xBB, 0xBF };

    static public JsonNode? GetRootJsonNode(Info info, string filePath)
    {
        try
        {
            FileInfo fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists)
            {
                return null;
            }

            ReadOnlySpan<byte> jsonReadOnlySpan = File.ReadAllBytes(filePath);
            // Read past the UTF-8 BOM bytes if a BOM exists.
            if (jsonReadOnlySpan.StartsWith(Utf8Bom))
            {
                jsonReadOnlySpan = jsonReadOnlySpan.Slice(Utf8Bom.Length);
            }

            return JsonNode.Parse(jsonReadOnlySpan);
        }
        catch (Exception ex)
        {
            info.Error($" * Exception while parsing {filePath}: {ex.Message}");
            return null;
        }
    }

    protected Info _info;

    public ClientApiConfigReader(Info info)
    {
        this._info = info;
    }

    public EndpointType fromFileOrEnvironment(string path)
    {
        fromFileJson(path);
        if (_type == EndpointType.IncompleteOptions)
        {
            fromEnvironment();
        }

        return _type;
    }

    public EndpointType fromFileJson(string path)
    {
        var rootNode = ClientApiConfigReader.GetRootJsonNode(_info, path);
        if (rootNode is not null)
        {
            ValueParser.AssertReadNodeStringParam(_info, rootNode, "AZURE_OPENAI_ENDPOINT", (value) => _aoaiEndpoint = value);
            ValueParser.AssertReadNodeBoolParam(_info, rootNode, "AZURE_OPENAI_USE_ENTRA", (value) => _aoaiUseEntra = value);
            ValueParser.AssertReadNodeStringParam(_info, rootNode, "AZURE_OPENAI_DEPLOYMENT", (value) => _aoaiDeployment = value);
            ValueParser.AssertReadNodeStringParam(_info, rootNode, "AZURE_OPENAI_API_KEY", (value) => _aoaiApiKey = value);
            ValueParser.AssertReadNodeStringParam(_info, rootNode, "OPENAI_API_KEY", (value) => _oaiApiKey = value);
        }
        return updateType(ConfigSource.ApiOptionsFromFile, path);
    }
}
