using LibRTIC.Config;
using LibRTIC.Realtime;
using OpenAI.Realtime;
using System.Text;
using Xunit;

namespace LibRTIC.Tests;

#pragma warning disable OPENAI002

public sealed class RTICConfigLoaderTests
{
    [Theory]
    [InlineData("rtic_api.openai.yaml", RTICProviderType.OpenAI)]
    [InlineData("rtic_api.azure-key.yaml", RTICProviderType.AzureOpenAIWithApiKey)]
    [InlineData("rtic_api.azure-entra.yaml", RTICProviderType.AzureOpenAIWithEntra)]
    public void ProviderExamples_Load(string fileName, RTICProviderType expectedType)
    {
        RTICConfigLoadResult result = RTICConfigLoader.LoadFile(Example(fileName));
        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics));
        Assert.Equal(expectedType, result.Config!.Provider.Type);
    }

    [Fact]
    public void SessionExample_LoadsMultilineUnicodeInstructionsAndVad()
    {
        string session = "instructions: |-\n  Dobar dan – こんにちは\nmax_output_tokens: 2048\nserver_vad:\n  threshold: 0.4\n  prefix_padding_ms: 200\n  silence_duration_ms: 800\n";
        RTICConfigLoadResult result = RTICConfigLoader.LoadFile(Example("rtic_api.openai.yaml"), Write("rtic_session.yaml", session));
        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics));
        Assert.Contains("こんにちは", result.Config!.Session.Instructions);
        Assert.Equal(2048, result.Config.Session.MaxOutputTokens);
        Assert.Equal(0.4f, result.Config.Session.ServerVad.Threshold);
    }

    [Theory]
    [InlineData("instructions: 42", "wrong_type")]
    [InlineData("instructions: null", "wrong_type")]
    [InlineData("max_output_tokens: \"2048\"", "wrong_type")]
    [InlineData("server_vad:\n  threshold: \"0.4\"", "wrong_type")]
    [InlineData("server_vad:\n  threshold: .nan", "wrong_type")]
    [InlineData("server_vad: []", "wrong_type")]
    [InlineData("unknown: value", "unknown_key")]
    [InlineData("Instructions: value", "unknown_key")]
    [InlineData("max_output_tokens: 0", "out_of_range")]
    [InlineData("max_output_tokens: 4097", "out_of_range")]
    [InlineData("server_vad:\n  prefix_padding_ms: -1", "out_of_range")]
    [InlineData("server_vad:\n  silence_duration_ms: 2001", "out_of_range")]
    public void SessionValidation_RejectsInvalidInputs(string session, string expectedCode)
    {
        RTICConfigLoadResult result = RTICConfigLoader.LoadFile(Example("rtic_api.openai.yaml"), Write("invalid.yaml", session));
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == expectedCode);
    }

    [Theory]
    [InlineData("provider:\n  type: openai\n  authentication:\n    type: api_key\n    api_key: true", "wrong_type")]
    [InlineData("provider:\n  type: openai\n  authentication:\n    type: api_key", "required_value")]
    [InlineData("provider:\n  type: openai\n  authentication:\n    type: entra\n    api_key: secret", "incompatible_value")]
    [InlineData("provider:\n  type: azure_openai\n  endpoint: https://example.openai.azure.com\n  deployment: dep\n  authentication:\n    type: entra\n    api_key: secret", "incompatible_value")]
    [InlineData("provider:\n  type: openai\n  authentication:\n    type: api_key\n    api_key: secret\n    api_key: duplicated", "duplicate_key")]
    [InlineData("provider:\n  type: openai\n  authentication: *missing", "unsupported_yaml_construct")]
    [InlineData("provider:\n  type: openai\n---\nprovider: {}", "multiple_documents")]
    public void ProviderValidation_RejectsInvalidInputsWithoutLeakingSecrets(string yaml, string expectedCode)
    {
        const string secret = "super-secret-value";
        yaml = yaml.Replace("secret", secret, StringComparison.Ordinal);
        RTICConfigLoadResult result = RTICConfigLoader.LoadFile(Write("invalid.yaml", yaml));
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == expectedCode);
        Assert.DoesNotContain(secret, string.Join(Environment.NewLine, result.Diagnostics));
    }

    [Fact]
    public void ExplicitAndAutomaticSources_AreAuthoritative()
    {
        string directory = Directory.CreateTempSubdirectory("librtic-").FullName;
        string api = Path.Combine(directory, RTICConfigLoader.DefaultApiConfigFileName);
        File.WriteAllText(api, OpenAiYaml("yaml-key"));
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", "environment-key");
        try
        {
            RTICConfigLoadResult automatic = RTICConfigLoader.LoadAuto(workingDirectory: directory);
            RTICConfigLoadResult explicitFile = RTICConfigLoader.LoadFile(api);
            Assert.Equal("yaml-key", Assert.IsType<OpenAIProviderOptions>(automatic.Config!.Provider).ApiKey);
            Assert.Equal("yaml-key", Assert.IsType<OpenAIProviderOptions>(explicitFile.Config!.Provider).ApiKey);

            File.WriteAllText(api, "provider: []");
            RTICConfigLoadResult invalidAutomatic = RTICConfigLoader.LoadAuto(workingDirectory: directory);
            Assert.Null(invalidAutomatic.Config);
            Assert.Contains(invalidAutomatic.Diagnostics, d => d.Code == "wrong_type");
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", null);
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void EnvironmentSource_IsUsedOnlyWhenNoDefaultYamlExists()
    {
        string directory = Directory.CreateTempSubdirectory("librtic-").FullName;
        string? openAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        string? endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
        string? deployment = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT");
        string? azureKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");
        string? entra = Environment.GetEnvironmentVariable("AZURE_OPENAI_USE_ENTRA");
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", "environment-key");
        Environment.SetEnvironmentVariable("AZURE_OPENAI_ENDPOINT", null);
        Environment.SetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT", null);
        Environment.SetEnvironmentVariable("AZURE_OPENAI_API_KEY", null);
        Environment.SetEnvironmentVariable("AZURE_OPENAI_USE_ENTRA", null);
        try
        {
            RTICConfigLoadResult result = RTICConfigLoader.LoadAuto(workingDirectory: directory);
            Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics));
            Assert.Equal("environment-key", Assert.IsType<OpenAIProviderOptions>(result.Config!.Provider).ApiKey);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", openAiKey);
            Environment.SetEnvironmentVariable("AZURE_OPENAI_ENDPOINT", endpoint);
            Environment.SetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT", deployment);
            Environment.SetEnvironmentVariable("AZURE_OPENAI_API_KEY", azureKey);
            Environment.SetEnvironmentVariable("AZURE_OPENAI_USE_ENTRA", entra);
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("config.yml", true)]
    [InlineData("config.json", false)]
    [InlineData("config.txt", false)]
    public void FileExtensions_AreHandledAsDocumented(string name, bool succeeds)
    {
        string path = Write(name, OpenAiYaml("key"));
        RTICConfigLoadResult result = RTICConfigLoader.LoadFile(path);
        Assert.Equal(succeeds, result.IsSuccess);
    }

    [Fact]
    public void Utf8WithAndWithoutBom_AreAccepted()
    {
        string yaml = OpenAiYaml("key");
        string withoutBom = Write("without-bom.yaml", yaml, new UTF8Encoding(false));
        string withBom = Write("with-bom.yaml", yaml, new UTF8Encoding(true));
        Assert.True(RTICConfigLoader.LoadFile(withoutBom).IsSuccess);
        Assert.True(RTICConfigLoader.LoadFile(withBom).IsSuccess);
    }

    [Fact]
    public void SessionFactory_MapsTheCompletePcmContractAndReturnsIndependentGraphs()
    {
        RealtimeConversationSessionOptions first = RealtimeSessionOptionsFactory.Create(RealtimeSessionOptionsFactory.Default);
        RealtimeConversationSessionOptions second = RealtimeSessionOptionsFactory.Create(RealtimeSessionOptionsFactory.Default);
        Assert.NotSame(first, second);
        Assert.NotSame(first.AudioOptions, second.AudioOptions);
        Assert.IsType<RealtimePcmAudioFormat>(first.AudioOptions.InputAudioOptions.AudioFormat);
        Assert.IsType<RealtimePcmAudioFormat>(first.AudioOptions.OutputAudioOptions.AudioFormat);
        Assert.Equal(RealtimeVoice.Alloy, first.AudioOptions.OutputAudioOptions.Voice);
        Assert.Equal("whisper-1", first.AudioOptions.InputAudioOptions.AudioTranscriptionOptions!.Model);
        RealtimeServerVadTurnDetection vad = Assert.IsType<RealtimeServerVadTurnDetection>(first.AudioOptions.InputAudioOptions.TurnDetection);
        Assert.True(vad.CreateResponseEnabled);
        Assert.False(vad.InterruptResponseEnabled);
        Assert.Equal(2048, first.MaxOutputTokenCount!.CustomMaxOutputTokenCount);
    }

    [Fact]
    public void ConsoleEntry_ResolvesRelativePathsAndAppVerbose()
    {
        string directory = Directory.CreateTempSubdirectory("librtic-entry-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(directory, "api.yaml"), OpenAiYaml("entry-key"));
            File.WriteAllText(
                Path.Combine(directory, "session.yaml"),
                "instructions: |-\n  Hello entry\nmax_output_tokens: 1024\n");
            string entry = Path.Combine(directory, RTICConfigLoader.DefaultConsoleEntryFileName);
            File.WriteAllText(entry,
                "realtime_client:\n" +
                "  api_config_path: \"./api.yaml\"\n" +
                "  session_config_path: \"./session.yaml\"\n" +
                "app:\n" +
                "  verbose: true\n");

            RTICConsoleConfigLoadResult result = RTICConfigLoader.LoadFromConsoleEntryFile(entry);
            Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics));
            Assert.Equal("entry-key", Assert.IsType<OpenAIProviderOptions>(result.Config!.Provider).ApiKey);
            Assert.Contains("Hello entry", result.Config.Session.Instructions);
            Assert.Equal(1024, result.Config.Session.MaxOutputTokens);
            Assert.True(result.App.Verbose);
            Assert.Equal(Path.GetFullPath(Path.Combine(directory, "api.yaml")), result.ResolvedApiConfigPath);
            Assert.Equal(Path.GetFullPath(Path.Combine(directory, "session.yaml")), result.ResolvedSessionConfigPath);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ConsoleEntry_DoesNotUseEnvironmentFallback()
    {
        string directory = Directory.CreateTempSubdirectory("librtic-entry-env-").FullName;
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", "environment-key");
        try
        {
            string entry = Path.Combine(directory, "rtic_console.yaml");
            File.WriteAllText(entry,
                "realtime_client:\n" +
                "  api_config_path: \"./missing-api.yaml\"\n" +
                "app:\n" +
                "  verbose: false\n");

            RTICConsoleConfigLoadResult result = RTICConfigLoader.LoadFromConsoleEntryFile(entry);
            Assert.False(result.IsSuccess);
            Assert.Null(result.Config);
            Assert.Contains(result.Diagnostics, d => d.Code == "file_not_found");
            Assert.False(result.App.Verbose);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", null);
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("app:\n  verbose: yes", "wrong_type")]
    [InlineData("app:\n  verbose: 1", "wrong_type")]
    [InlineData("realtime_client:\n  api_config_path: ./a.yaml\nunknown: x", "unknown_key")]
    [InlineData("realtime_client: {}", "required_value")]
    [InlineData("app:\n  verbose: true", "required_value")]
    public void ConsoleEntry_RejectsInvalidInputs(string yaml, string expectedCode)
    {
        string path = Write("rtic_console.yaml", yaml);
        RTICConsoleConfigLoadResult result = RTICConfigLoader.LoadFromConsoleEntryFile(path);
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == expectedCode);
    }

    [Theory]
    [InlineData(new string[0], RTICConsoleHostArguments.Mode.Legacy, null)]
    [InlineData(new[] { "--api", "a.yaml" }, RTICConsoleHostArguments.Mode.Legacy, null)]
    [InlineData(new[] { "--config", "host.yaml" }, RTICConsoleHostArguments.Mode.ExclusiveConfig, "host.yaml")]
    [InlineData(new[] { "-c", "host.yaml" }, RTICConsoleHostArguments.Mode.ExclusiveConfig, "host.yaml")]
    [InlineData(new[] { "--config" }, RTICConsoleHostArguments.Mode.Invalid, null)]
    [InlineData(new[] { "--config", "host.yaml", "--verbose" }, RTICConsoleHostArguments.Mode.Invalid, null)]
    [InlineData(new[] { "--api", "a.yaml", "--config", "host.yaml" }, RTICConsoleHostArguments.Mode.Invalid, null)]
    public void ConsoleHostArguments_ClassifyExclusiveConfig(
        string[] args,
        RTICConsoleHostArguments.Mode expectedMode,
        string? expectedPath)
    {
        RTICConsoleHostArguments.Mode mode = RTICConsoleHostArguments.Classify(args, out string? path, out string? error);
        Assert.Equal(expectedMode, mode);
        Assert.Equal(expectedPath, path);
        if (expectedMode == RTICConsoleHostArguments.Mode.Invalid)
        {
            Assert.False(string.IsNullOrWhiteSpace(error));
        }
        else
        {
            Assert.Null(error);
        }
    }

    private static string Example(string name) => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "docs", name));
    private static string OpenAiYaml(string key) => $"provider:\n  type: openai\n  authentication:\n    type: api_key\n    api_key: {key}\n";
    private static string Write(string name, string contents, Encoding? encoding = null)
    {
        string directory = Directory.CreateTempSubdirectory("librtic-").FullName;
        string path = Path.Combine(directory, name);
        File.WriteAllText(path, contents, encoding ?? new UTF8Encoding(false));
        return path;
    }
}
