using Azure.AI.OpenAI;
using Azure.Identity;
using OpenAI.RealtimeConversation;
using OpenAI;
using System.ClientModel;
using LibRTIC.MiniTaskLib.Model;

namespace LibRTIC.Config;

#pragma warning disable OPENAI002

public class ConfiguredClient
{
    public const string DEFAULT_CONVERSATIONAPI_FILENAME = "realtime_api.conf";

    public static RealtimeConversationClient? FromOptions(Info info, ClientApiConfig options)
    {
        switch (options.Type)
        {
            case EndpointType.AzureOpenAIWithEntra:
                return ForAzureOpenAIWithEntra(info, options.AOAIEndpoint, options.AOAIDeployment);

            case EndpointType.AzureOpenAIWithKey:
                return ForAzureOpenAIWithKey(info, options.AOAIEndpoint, options.AOAIDeployment, options.AOAIApiKey);

            case EndpointType.OpenAIWithKey:
                return ForOpenAIWithKey(info, options.OAIApiKey);
        }

        info.Error(
                    $"Incomplete or missing '" + DEFAULT_CONVERSATIONAPI_FILENAME + "' or environment configuration.Please provide one of:\n"
                    + " - AZURE_OPENAI_ENDPOINT with AZURE_OPENAI_USE_ENTRA=true or AZURE_OPENAI_API_KEY\n"
                    + " - OPENAI_API_KEY");
        return null;
    }

    private static RealtimeConversationClient ForAzureOpenAIWithEntra(Info info, string aoaiEndpoint, string? aoaiDeployment)
    {
        info.Info($" * Connecting to Azure OpenAI endpoint (AZURE_OPENAI_ENDPOINT): {aoaiEndpoint}");
        info.Info($" * Using Entra token-based authentication (AZURE_OPENAI_USE_ENTRA)");
        info.Info(string.IsNullOrEmpty(aoaiDeployment)
                                 ? $" * Using no deployment (AZURE_OPENAI_DEPLOYMENT)"
                                 : $" * Using deployment (AZURE_OPENAI_DEPLOYMENT): {aoaiDeployment}");

        AzureOpenAIClient aoaiClient = new(new Uri(aoaiEndpoint), new DefaultAzureCredential());
        return aoaiClient.GetRealtimeConversationClient(aoaiDeployment);
    }

    private static RealtimeConversationClient ForAzureOpenAIWithKey(Info info, string aoaiEndpoint, string? aoaiDeployment, string aoaiApiKey)
    {
        info.Info($" * Connecting to Azure OpenAI endpoint (AZURE_OPENAI_ENDPOINT): {aoaiEndpoint}");
        info.Info($" * Using API key (AZURE_OPENAI_API_KEY): {aoaiApiKey[..5]}**");
        info.Info(string.IsNullOrEmpty(aoaiDeployment)
                                 ? $" * Using no deployment (AZURE_OPENAI_DEPLOYMENT)"
                                 : $" * Using deployment (AZURE_OPENAI_DEPLOYMENT): {aoaiDeployment}");

        AzureOpenAIClient aoaiClient = new(new Uri(aoaiEndpoint), new ApiKeyCredential(aoaiApiKey));
        return aoaiClient.GetRealtimeConversationClient(aoaiDeployment);
    }

    private static RealtimeConversationClient ForOpenAIWithKey(Info info, string oaiApiKey)
    {
        string oaiEndpoint = "https://api.openai.com/v1";
        info.Info($" * Connecting to OpenAI endpoint (OPENAI_ENDPOINT): {oaiEndpoint}");
        info.Info($" * Using API key (OPENAI_API_KEY): {oaiApiKey[..5]}**");

        OpenAIClient aoaiClient = new(new ApiKeyCredential(oaiApiKey));
        return aoaiClient.GetRealtimeConversationClient("gpt-4o-realtime-preview-2024-10-01");
    }
}
