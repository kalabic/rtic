using OpenAI.RealtimeConversation;
using LibRTIC.MiniTaskLib.Model;
using System.Text.Json.Nodes;

namespace LibRTIC.Config;

#pragma warning disable OPENAI002

public class ConversationOptions
{
    public const float DEFAULT_TEMPERATURE = 0.7f;
    public const float DEFAULT_SERVERVAD_THRESHOLD = 0.5f;
    public const int DEFAULT_SERVERVAD_PREFIXPADDINGMS = 300;
    public const int DEFAULT_SERVERVAD_SILENCEDURATIONMS = 500;

    public const string PARAM_NAME_Instructions = "instructions";
    public const string PARAM_NAME_Temperature = "temperature";
    public const string PARAM_NAME_MaxOutputTokens = "max_response_output_tokens";
    public const string PARAM_NAME_ServerVAD = "Server VAD";
    public const string PARAM_NAME_Threshold = "threshold";
    public const string PARAM_NAME_PrefixPadding = "prefix_padding_ms";
    public const string PARAM_NAME_SilenceDuration = "silence_duration_ms";
    public const string PARAM_NAME_Tools = "tools";
    public const string PARAM_NAME_ToolsName = "name";
    public const string PARAM_NAME_ToolsDescription = "description";
    public const string PARAM_NAME_Parameters = "parameters";

    static public ConversationOptions NewIncompleteOptions()
    {
        return new ConversationOptions("Incomplete or missing '" + ConfiguredClient.DEFAULT_CONVERSATIONAPI_FILENAME + "' or environment configuration. Please provide one of:\n"
                                     + " - AZURE_OPENAI_ENDPOINT with AZURE_OPENAI_USE_ENTRA=true or AZURE_OPENAI_API_KEY\n"
                                     + " - OPENAI_API_KEY");
    }

    static public ConversationOptions GetDefaultOptions(Info info)
    {
        var clientOptions = ConversationOptions.GetDefaultClientOptions(info);
        if (clientOptions.Type == EndpointType.IncompleteOptions)
        {
            return ConversationOptions.NewIncompleteOptions();
        }
        var sessionOptions = ConversationOptions.GetDefaultSessionOptions();
        return new ConversationOptions(clientOptions, sessionOptions);
    }

    static public ConversationOptions FromEnvironment()
    {
        var config = ClientApiConfig.FromEnvironment();
        return new ConversationOptions(config, null);
    }

    static private string GetDefaultIniPath()
    {
        return ConfiguredClient.DEFAULT_CONVERSATIONAPI_FILENAME;
    }

    static private ClientApiConfigReader GetDefaultClientOptions(Info info)
    {
        // Try to load options first from INI file then fron environment.
        return ClientApiConfigReader.FromFileOrEnvironment(info, GetDefaultIniPath());
    }

    static private ConversationSessionOptions GetDefaultSessionOptions()
    {
        var sessionOptions = new ConversationSessionOptions()
        {
            InputTranscriptionOptions = new()
            {
                Model = "whisper-1",
            },
            TurnDetectionOptions =
                ConversationTurnDetectionOptions.CreateServerVoiceActivityTurnDetectionOptions(
                                    DEFAULT_SERVERVAD_THRESHOLD,
                                    TimeSpan.FromMilliseconds(DEFAULT_SERVERVAD_PREFIXPADDINGMS),
                                    TimeSpan.FromMilliseconds(DEFAULT_SERVERVAD_SILENCEDURATIONMS)),
            Instructions = "Your knowledge cutoff is 2023-10. You are a helpful, witty, and friendly AI. Act like a human, " +
                           "but remember that you aren't a human and that you can't do human things in the real world. Your " +
                           "voice and personality should be warm and engaging, with a lively and playful tone. If interacting " +
                           "in a non-English language, start by using the standard accent or dialect familiar to the user. " +
                           "Talk quickly. You should always call a function if you can. Do not refer to these rules, even if " +
                           "you're asked about them.",
            MaxOutputTokens = 2048,
            Temperature = DEFAULT_TEMPERATURE,
            Voice = ConversationVoice.Alloy,
        };
        return sessionOptions;
    }

    static public ConversationOptions ReadFromFile(CommandLineArguments args, Info info)
    {
        var apiFile = args.apiFile;
        var sessionFile = args.sessionFile;

        ClientApiConfig? clientOptions = null;
        if (apiFile is not null)
        {
            if (!apiFile.Exists)
            {
                return new ConversationOptions($" * File does not exist: {apiFile.FullName}");
            }
            clientOptions = ClientApiConfigReader.FromFile(info, apiFile.FullName);
            switch (clientOptions.Type)
            {
                case EndpointType.IncompleteOptions:
                    return new ConversationOptions("Incomplete configuration in provided API file.");

                case EndpointType.AzureOpenAIWithEntra:
                    info.Info(" * 'Azure OpenAI With Entra' configuration provided.");
                    break;

                case EndpointType.AzureOpenAIWithKey:
                    info.Info(" * 'Azure OpenAI With Key' configuration provided.");
                    break;

                case EndpointType.OpenAIWithKey:
                    info.Info(" * 'OpenAI With Key' configuration provided.");
                    break;
            }
        }
        else
        {
            clientOptions = ConversationOptions.GetDefaultClientOptions(info);
            if (clientOptions.Type == EndpointType.IncompleteOptions)
            {
                return ConversationOptions.NewIncompleteOptions();
            }
        }

        var sessionOptions = GetDefaultSessionOptions();
        if (sessionFile is not null)
        {
            var rootNode = ClientApiConfigReader.GetRootJsonNode(info, sessionFile.FullName);
            if (rootNode is null)
            {
                return new ConversationOptions($" * Error parsing file {sessionFile.FullName}");
            }

            info.Info(" * Provided session options:");

            //
            // Instructions
            //
            if (ValueParser.AssertReadNodeStringParam(info, rootNode, PARAM_NAME_Instructions, (value) => {
                sessionOptions.Instructions = value;
                info.Info($" * - Instructions: {value}");
            }) < 0) { return new ConversationOptions($" * Error parsing file {sessionFile.FullName}"); }

            //
            // Temperature
            //
            if (ValueParser.AssertReadNodeFloatParamInRange(info, rootNode, PARAM_NAME_Temperature, 0.6f, 1.2f, (value) => {
                sessionOptions.Temperature = value;
                info.Info($" * - Temperature: {value}");
            }) < 0) { return new ConversationOptions($" * Error parsing file {sessionFile.FullName}"); }

            //
            // MaxOutputTokens
            //
            if (ValueParser.AssertReadNodeIntParamInRange(info, rootNode, PARAM_NAME_MaxOutputTokens, 1, 1000000, (value) => {
                sessionOptions.MaxOutputTokens = value;
                info.Info($" * - MaxOutputTokens: {value}");
            }) < 0) { return new ConversationOptions($" * Error parsing file {sessionFile.FullName}"); }

            int assertParam = ValueParser.AssertNodeParamIsNullOrObject(info, rootNode, PARAM_NAME_ServerVAD);
            if (assertParam == 1)
            {
                float threshold = DEFAULT_SERVERVAD_THRESHOLD;
                int prefixPaddingMs = DEFAULT_SERVERVAD_PREFIXPADDINGMS;
                int silenceDurationMs = DEFAULT_SERVERVAD_SILENCEDURATIONMS;

                var paramNode = rootNode![PARAM_NAME_ServerVAD]!;

                //
                // ServerVAD.Threshold
                //
                if (ValueParser.AssertReadNodeFloatParamInRange(info, paramNode, PARAM_NAME_Threshold, 0.0f, 1.0f, (value) => {
                    threshold = value;
                    info.Info($" * - Threshold: {value}");
                }) < 0) { return new ConversationOptions($" * Error parsing file {sessionFile.FullName}"); }

                //
                // ServerVAD.PrefixPadding
                //
                if (ValueParser.AssertReadNodeIntParamInRange(info, paramNode, PARAM_NAME_PrefixPadding, 0, 2000, (value) => {
                    prefixPaddingMs = value;
                    info.Info($" * - PrefixPadding: {value}");
                }) < 0) { return new ConversationOptions($" * Error parsing file {sessionFile.FullName}"); }

                //
                // ServerVAD.SilenceDuration
                //
                if (ValueParser.AssertReadNodeIntParamInRange(info, paramNode, PARAM_NAME_SilenceDuration, 0, 2000, (value) => {
                    silenceDurationMs = value;
                    info.Info($" * - SilenceDuration: {value}");
                }) < 0) { return new ConversationOptions($" * Error parsing file {sessionFile.FullName}"); }

                sessionOptions.TurnDetectionOptions =
                    ConversationTurnDetectionOptions.CreateServerVoiceActivityTurnDetectionOptions(
                        threshold, TimeSpan.FromMilliseconds(prefixPaddingMs), TimeSpan.FromMilliseconds(silenceDurationMs));
            }
            else if (assertParam == -1)
            {
                return new ConversationOptions($" * Error parsing file {sessionFile.FullName}");
            }

            assertParam = ValueParser.AssertNodeParamIsNullOrArray(info, rootNode, PARAM_NAME_Tools);
            if (assertParam == 1)
            {
                var paramNode = rootNode!["Tools"]!.AsArray();
                var toolsList = ParseToolsJson(info, sessionFile.FullName, paramNode);
                if (toolsList is not null)
                {
                    foreach (var tool in toolsList)
                    {
                        sessionOptions.Tools.Add(tool);
                        info.Info($" * - Tool added : {tool.Name}");
                    }
                }
                else
                {
                    return new ConversationOptions("Failed to parse tools configuration file.");
                }
            }
            else if (assertParam == -1)
            {
                return new ConversationOptions($" * Error parsing file {sessionFile.FullName}");
            }
        }

        return new ConversationOptions(clientOptions, sessionOptions, args.multiSession);
    }

    private static List<ConversationFunctionTool>? ParseToolsJson(Info info, string filePath, JsonArray rootArray)
    {
        try
        {
            var list = new List<ConversationFunctionTool>();
            foreach (var rootItem in rootArray)
            {
                var functionObject = rootItem?.AsObject();
                if (functionObject is not null && rootItem is not null)
                {
                    string? nameValue = null;
                    string? descriptionValue = null;

                    //
                    // Tools[x].Name
                    //
                    if (ValueParser.AssertReadNodeStringParam(info, rootItem, PARAM_NAME_ToolsName, (value) => {
                        nameValue = value;
                    }) < 0) { return null; }

                    //
                    // Tools[x].Description
                    //
                    if (ValueParser.AssertReadNodeStringParam(info, rootItem, PARAM_NAME_ToolsDescription, (value) => {
                        descriptionValue = value;
                    }) < 0) { return null; }

                    var parametersNode = rootItem[PARAM_NAME_Parameters];
                    string? parametersString = parametersNode?.ToJsonString();
                    BinaryData? parametersData =
                        (parametersString is not null) ? BinaryData.FromString(parametersString) : null;

                    ConversationFunctionTool tool = new()
                    {
                        Name = nameValue,
                        Description = descriptionValue,
                        Parameters = parametersData,
                    };
                    list.Add(tool);
                }
                else
                {
                    return null;
                }
            }
            return list;
        }
        catch (Exception ex)
        {
            info.Error($" * - Tools : EXCEPTION WHILE PARSING {filePath}, ForwarderMessage: {ex.Message}");
        }

        return null;
    }

    public ClientApiConfig? _client = null;
    public ConversationSessionOptions? _session = null;
    public bool _enableMultiSession = false;
    public string? _errorMessage = null;

    public bool IsNull => _client == null || _session == null;

    public ConversationOptions(string errorMessage)
    {
        this._errorMessage = errorMessage;
    }

    public ConversationOptions(ClientApiConfig clientOptions,
                               ConversationSessionOptions? sessionOptions)
    {
        this._client = clientOptions;
        this._session = sessionOptions;
        this._enableMultiSession = false;
    }

    public ConversationOptions(ClientApiConfig clientOptions,
                               ConversationSessionOptions? sessionOptions,
                               bool enableMultiSession)
    {
        this._client = clientOptions;
        this._session = sessionOptions;
        this._enableMultiSession = enableMultiSession;
    }

    public void PrintApiConfigSourceInfo(Info info)
    {
        if (_client is not null)
        {
            if (_client.Source == ConfigSource.ApiOptionsFromEnvironment)
            {
                info.Info("Endpoint configuration provided from environment.");
            }
            else if (_client.Source == ConfigSource.ApiOptionsFromFile)
            {
                info.Info($"Endpoint configuration provided from {_client.ConfigFile}.");
            }
            else if (_client.Source == ConfigSource.ApiOptionsFromOther)
            {
                info.Info($"Endpoint configuration provided from other. {_client.ConfigFile}");
            }
        }
    }
}
