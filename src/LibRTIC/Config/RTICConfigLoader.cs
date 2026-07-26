using System.Globalization;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace LibRTIC.Config;

public sealed class RTICConfigLoadResult
{
    public RTICConfig? Config { get; }
    public IReadOnlyList<ConfigDiagnostic> Diagnostics { get; }
    public bool IsSuccess => Config is not null && Diagnostics.All(d => d.Severity != ConfigDiagnosticSeverity.Error);
    internal RTICConfigLoadResult(RTICConfig? config, List<ConfigDiagnostic> diagnostics) => (Config, Diagnostics) = (config, diagnostics);
}

public enum ConfigDiagnosticSeverity { Error, Warning }

public sealed record ConfigDiagnostic(string Code, string? FileName, string Path, int? Line, int? Column, string Message, ConfigDiagnosticSeverity Severity = ConfigDiagnosticSeverity.Error)
{
    public override string ToString() => $"{Code}: {FileName ?? "configuration"}{Path} ({Line}:{Column}): {Message}";
}

/// <summary>Strict, non-merging YAML and environment configuration loader.</summary>
public static class RTICConfigLoader
{
    public const string DefaultApiConfigFileName = "rtic_api.yaml";
    public const string LegacyApiConfigFileName = "rtic_api.json";

    /// <summary>Conventional console host entry file name (RTIConsole / WinRTIC).</summary>
    public const string DefaultConsoleEntryFileName = RTICConsoleHostArguments.DefaultConsoleEntryFileName;

    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .WithDuplicateKeyChecking()
        .WithEnforceNullability()
        .Build();

    /// <summary>
    /// Loads an authoritative console host entry file (e.g. <c>rtic_console.yaml</c>),
    /// resolves <c>realtime_client</c> paths relative to that file, and loads provider/session YAML.
    /// Does not fall back to environment variables or CWD auto-discovery.
    /// </summary>
    public static RTICConsoleConfigLoadResult LoadFromConsoleEntryFile(string entryPath)
    {
        var diagnostics = new List<ConfigDiagnostic>();
        if (string.IsNullOrWhiteSpace(entryPath))
        {
            Add(diagnostics, "entry_path_required", null, "$", null, "A console host entry YAML file is required.");
            return new RTICConsoleConfigLoadResult(null, RTICConsoleAppOptions.Default, null, null, diagnostics);
        }

        string fullEntryPath = Path.GetFullPath(entryPath);
        if (!TryDeserialize<ConsoleEntryConfigDocument>(fullEntryPath, diagnostics, out ConsoleEntryConfigDocument? document, out YamlMarks marks))
        {
            return new RTICConsoleConfigLoadResult(null, RTICConsoleAppOptions.Default, null, null, diagnostics);
        }

        ConsoleEntryConfigDocument.RealtimeClientMapping? client = document!.RealtimeClient;
        if (client is null)
        {
            Required(diagnostics, fullEntryPath, "$.realtime_client", marks);
            return new RTICConsoleConfigLoadResult(null, RTICConsoleAppOptions.Default, null, null, diagnostics);
        }

        string? apiRelative = RequiredString(client.ApiConfigPath, diagnostics, fullEntryPath, "$.realtime_client.api_config_path", marks);
        string? sessionRelative = OptionalString(client.SessionConfigPath, diagnostics, fullEntryPath, "$.realtime_client.session_config_path", marks);

        RTICConsoleAppOptions app = RTICConsoleAppOptions.Default;
        if (document.App is { } appMapping)
        {
            bool verbose = false;
            if (marks.Contains("$.app.verbose"))
            {
                if (appMapping.Verbose is null)
                {
                    Add(diagnostics, "wrong_type", fullEntryPath, "$.app.verbose", marks.At("$.app.verbose"), "A boolean is required.");
                }
                else
                {
                    verbose = appMapping.Verbose.Value;
                }
            }

            app = new RTICConsoleAppOptions(verbose);
        }

        if (HasErrors(diagnostics) || apiRelative is null)
        {
            return new RTICConsoleConfigLoadResult(null, app, null, null, diagnostics);
        }

        string entryDirectory = Path.GetDirectoryName(fullEntryPath) ?? Directory.GetCurrentDirectory();
        string resolvedApi = ResolvePathAgainstEntry(entryDirectory, apiRelative);
        string? resolvedSession = sessionRelative is null
            ? null
            : ResolvePathAgainstEntry(entryDirectory, sessionRelative);

        RTICConfigLoadResult composed = LoadFile(resolvedApi, resolvedSession);
        diagnostics.AddRange(composed.Diagnostics);

        return new RTICConsoleConfigLoadResult(
            composed.Config,
            app,
            resolvedApi,
            resolvedSession,
            diagnostics);
    }

    private static string ResolvePathAgainstEntry(string entryDirectory, string configuredPath)
    {
        string expanded = Environment.ExpandEnvironmentVariables(configuredPath.Trim());
        if (Path.IsPathRooted(expanded))
        {
            return Path.GetFullPath(expanded);
        }

        return Path.GetFullPath(Path.Combine(entryDirectory, expanded));
    }

    public static RTICConfigLoadResult LoadAuto(string? sessionPath = null, string? workingDirectory = null)
    {
        var diagnostics = new List<ConfigDiagnostic>();
        string root = workingDirectory ?? Directory.GetCurrentDirectory();
        string defaultYaml = Path.Combine(root, DefaultApiConfigFileName);
        string legacyJson = Path.Combine(root, LegacyApiConfigFileName);
        if (File.Exists(defaultYaml)) return Complete(LoadProviderFile(defaultYaml, diagnostics), sessionPath, diagnostics);
        if (File.Exists(legacyJson))
        {
            Add(diagnostics, "legacy_json_not_supported", legacyJson, "$", null, "JSON configuration is no longer supported; migrate to rtic_api.yaml (see docs/README.md).");
            return Complete(null, sessionPath, diagnostics);
        }
        return Complete(LoadEnvironmentProvider(diagnostics), sessionPath, diagnostics);
    }

    public static RTICConfigLoadResult LoadFile(string providerPath, string? sessionPath = null)
    {
        var diagnostics = new List<ConfigDiagnostic>();
        if (string.IsNullOrWhiteSpace(providerPath))
            Add(diagnostics, "provider_path_required", null, "$", null, "An API YAML file is required.");
        return Complete(string.IsNullOrWhiteSpace(providerPath) ? null : LoadProviderFile(providerPath, diagnostics), sessionPath, diagnostics);
    }

    public static RTICConfigLoadResult LoadEnvironment(string? sessionPath = null)
    {
        var diagnostics = new List<ConfigDiagnostic>();
        return Complete(LoadEnvironmentProvider(diagnostics), sessionPath, diagnostics);
    }

    private static RTICConfigLoadResult Complete(RTICProviderOptions? provider, string? sessionPath, List<ConfigDiagnostic> diagnostics)
    {
        RTICSessionOptions session = Realtime.RealtimeSessionOptionsFactory.Default;
        if (!string.IsNullOrWhiteSpace(sessionPath)) session = LoadSessionFile(sessionPath, diagnostics) ?? session;
        return diagnostics.Any(d => d.Severity == ConfigDiagnosticSeverity.Error) || provider is null
            ? new RTICConfigLoadResult(null, diagnostics)
            : new RTICConfigLoadResult(new RTICConfig(provider, session), diagnostics);
    }

    private static RTICProviderOptions? LoadEnvironmentProvider(List<ConfigDiagnostic> diagnostics)
    {
        string? endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
        string? deployment = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT");
        string? key = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");
        string? entraText = Environment.GetEnvironmentVariable("AZURE_OPENAI_USE_ENTRA");
        bool azureIntent = new[] { endpoint, deployment, key, entraText }.Any(v => !string.IsNullOrWhiteSpace(v));
        if (azureIntent)
        {
            bool entra = string.Equals(entraText, "true", StringComparison.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(entraText) && !bool.TryParse(entraText, out _)) Add(diagnostics, "invalid_environment_value", null, "$.AZURE_OPENAI_USE_ENTRA", null, "AZURE_OPENAI_USE_ENTRA must be true or false.");
            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? endpointUri)) Add(diagnostics, "required_value", null, "$.AZURE_OPENAI_ENDPOINT", null, "Azure endpoint must be an absolute URI.");
            if (string.IsNullOrWhiteSpace(deployment)) Add(diagnostics, "required_value", null, "$.AZURE_OPENAI_DEPLOYMENT", null, "Azure deployment is required.");
            if (entra && !string.IsNullOrWhiteSpace(key)) Add(diagnostics, "incompatible_value", null, "$.AZURE_OPENAI_API_KEY", null, "Entra authentication cannot include an API key.");
            if (!entra && string.IsNullOrWhiteSpace(key)) Add(diagnostics, "required_value", null, "$.AZURE_OPENAI_API_KEY", null, "Azure API-key authentication requires AZURE_OPENAI_API_KEY.");
            if (diagnostics.Any(d => d.Severity == ConfigDiagnosticSeverity.Error)) return null;
            return entra ? new AzureOpenAIEntraProviderOptions(endpointUri!, deployment!) : new AzureOpenAIApiKeyProviderOptions(endpointUri!, deployment!, key!);
        }
        string? openAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(openAiKey)) { Add(diagnostics, "provider_not_configured", null, "$", null, "Set OPENAI_API_KEY or a complete AZURE_OPENAI_* configuration."); return null; }
        return new OpenAIProviderOptions(openAiKey);
    }

    private static RTICProviderOptions? LoadProviderFile(string path, List<ConfigDiagnostic> diagnostics)
    {
        if (!TryDeserialize<ApiConfigDocument>(path, diagnostics, out ApiConfigDocument? document, out YamlMarks marks)) return null;
        ApiConfigDocument.ProviderMapping? provider = document!.Provider;
        if (provider is null) { Required(diagnostics, path, "$.provider", marks); return null; }
        string? type = RequiredString(provider.Type, diagnostics, path, "$.provider.type", marks);
        ApiConfigDocument.AuthenticationMapping? authentication = provider.Authentication;
        if (authentication is null) { Required(diagnostics, path, "$.provider.authentication", marks); return null; }
        string? authenticationType = RequiredString(authentication.Type, diagnostics, path, "$.provider.authentication.type", marks);

        if (type == "openai")
        {
            string? key = RequiredString(authentication.ApiKey, diagnostics, path, "$.provider.authentication.api_key", marks);
            if (authenticationType != "api_key") Add(diagnostics, "incompatible_value", path, "$.provider.authentication.type", marks.At("$.provider.authentication.type"), "OpenAI requires api_key authentication.");
            return HasErrors(diagnostics) ? null : new OpenAIProviderOptions(key!);
        }
        if (type == "azure_openai")
        {
            string? endpointText = RequiredString(provider.Endpoint, diagnostics, path, "$.provider.endpoint", marks);
            string? deployment = RequiredString(provider.Deployment, diagnostics, path, "$.provider.deployment", marks);
            string? key = OptionalString(authentication.ApiKey, diagnostics, path, "$.provider.authentication.api_key", marks);
            if (!Uri.TryCreate(endpointText, UriKind.Absolute, out Uri? endpoint)) Add(diagnostics, "invalid_value", path, "$.provider.endpoint", marks.At("$.provider.endpoint"), "Endpoint must be an absolute URI.");
            if (authenticationType == "api_key" && string.IsNullOrWhiteSpace(key)) Add(diagnostics, "required_value", path, "$.provider.authentication.api_key", marks.At("$.provider.authentication.api_key"), "API-key authentication requires api_key.");
            if (authenticationType == "entra" && key is not null) Add(diagnostics, "incompatible_value", path, "$.provider.authentication.api_key", marks.At("$.provider.authentication.api_key"), "Entra authentication forbids api_key.");
            if (authenticationType is not ("api_key" or "entra")) Add(diagnostics, "invalid_value", path, "$.provider.authentication.type", marks.At("$.provider.authentication.type"), "Authentication type must be api_key or entra.");
            return HasErrors(diagnostics) ? null : authenticationType == "entra" ? new AzureOpenAIEntraProviderOptions(endpoint!, deployment!) : new AzureOpenAIApiKeyProviderOptions(endpoint!, deployment!, key!);
        }
        Add(diagnostics, "invalid_value", path, "$.provider.type", marks.At("$.provider.type"), "Provider type must be openai or azure_openai.");
        return null;
    }

    private static RTICSessionOptions? LoadSessionFile(string path, List<ConfigDiagnostic> diagnostics)
    {
        if (!TryDeserialize<SessionConfigDocument>(path, diagnostics, out SessionConfigDocument? document, out YamlMarks marks)) return null;
        RTICSessionOptions defaults = Realtime.RealtimeSessionOptionsFactory.Default;
        string instructions = marks.Contains("$.instructions") ? RequiredString(document!.Instructions, diagnostics, path, "$.instructions", marks) ?? defaults.Instructions : defaults.Instructions;
        int tokens = document!.MaxOutputTokens ?? defaults.MaxOutputTokens;
        if (tokens is < 1 or > 4096) Add(diagnostics, "out_of_range", path, "$.max_output_tokens", marks.At("$.max_output_tokens"), "max_output_tokens must be from 1 through 4096.");
        ServerVadOptions vad = defaults.ServerVad;
        if (document.ServerVad is { } rawVad)
        {
            float threshold = rawVad.Threshold ?? vad.Threshold;
            int prefix = rawVad.PrefixPaddingMs ?? vad.PrefixPaddingMs;
            int silence = rawVad.SilenceDurationMs ?? vad.SilenceDurationMs;
            if (threshold is < 0 or > 1) Add(diagnostics, "out_of_range", path, "$.server_vad.threshold", marks.At("$.server_vad.threshold"), "threshold must be from 0 through 1.");
            if (prefix is < 0 or > 2000) Add(diagnostics, "out_of_range", path, "$.server_vad.prefix_padding_ms", marks.At("$.server_vad.prefix_padding_ms"), "prefix_padding_ms must be from 0 through 2000.");
            if (silence is < 0 or > 2000) Add(diagnostics, "out_of_range", path, "$.server_vad.silence_duration_ms", marks.At("$.server_vad.silence_duration_ms"), "silence_duration_ms must be from 0 through 2000.");
            if (!HasErrors(diagnostics)) vad = new ServerVadOptions(threshold, prefix, silence);
        }
        return HasErrors(diagnostics) ? null : new RTICSessionOptions(instructions, tokens, vad);
    }

    private static bool TryDeserialize<T>(string path, List<ConfigDiagnostic> diagnostics, out T? document, out YamlMarks marks)
    {
        document = default;
        marks = new YamlMarks();
        if (!ValidateExtensionAndFile(path, diagnostics, out string? yaml)) return false;
        if (!ScanYaml(yaml!, path, diagnostics, marks)) return false;
        if (!ValidateScalarTypes(marks, path, diagnostics)) return false;
        try { document = Deserializer.Deserialize<T>(yaml!); return true; }
        catch (YamlException exception)
        {
            bool isUnknownProperty = exception.Message.Contains("Property", StringComparison.OrdinalIgnoreCase)
                && exception.Message.Contains("not found", StringComparison.OrdinalIgnoreCase);
            Add(diagnostics, isUnknownProperty ? "unknown_key" : "invalid_yaml", path, marks.PathAt(exception.Start), exception.Start, isUnknownProperty ? "Unknown configuration key." : "YAML does not match the configuration schema.");
            return false;
        }
        catch (Exception) { Add(diagnostics, "invalid_yaml", path, "$", null, "YAML does not match the configuration schema."); return false; }
    }

    private static bool ValidateExtensionAndFile(string path, List<ConfigDiagnostic> diagnostics, out string? yaml)
    {
        yaml = null;
        if (Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase)) { Add(diagnostics, "legacy_json_not_supported", path, "$", null, "JSON configuration is no longer supported; migrate to YAML (see docs/README.md)."); return false; }
        if (!Path.GetExtension(path).Equals(".yaml", StringComparison.OrdinalIgnoreCase) && !Path.GetExtension(path).Equals(".yml", StringComparison.OrdinalIgnoreCase)) { Add(diagnostics, "unsupported_extension", path, "$", null, "Configuration files must use .yaml or .yml."); return false; }
        if (!File.Exists(path)) { Add(diagnostics, "file_not_found", path, "$", null, "Configuration file was not found."); return false; }
        try { yaml = File.ReadAllText(path); return true; }
        catch (Exception) { Add(diagnostics, "read_error", path, "$", null, "Unable to read configuration file."); return false; }
    }

    // This event pass deliberately records only YAML locations and rejects YAML
    // constructs that do not belong to the small configuration language.
    private static bool ScanYaml(string yaml, string path, List<ConfigDiagnostic> diagnostics, YamlMarks marks)
    {
        try
        {
            var parser = new Parser(new StringReader(yaml));
            parser.MoveNext();
            if (parser.Current is not StreamStart) throw new YamlException("Missing stream start.");
            parser.MoveNext();
            if (parser.Current is not DocumentStart) { Add(diagnostics, "invalid_document", path, "$", MarkOf(parser.Current), "A single YAML document is required."); return false; }
            parser.MoveNext();
            if (parser.Current is not MappingStart) { Add(diagnostics, "wrong_root_type", path, "$", MarkOf(parser.Current), "The YAML root must be a mapping."); return false; }
            ScanMapping(parser, path, "$", diagnostics, marks);
            if (parser.Current is not DocumentEnd) { Add(diagnostics, "invalid_document", path, "$", MarkOf(parser.Current), "A single YAML document is required."); return false; }
            parser.MoveNext();
            if (parser.Current is not StreamEnd) { Add(diagnostics, "multiple_documents", path, "$", MarkOf(parser.Current), "Only one YAML document is accepted."); return false; }
            return !HasErrors(diagnostics);
        }
        catch (YamlException ex) { Add(diagnostics, "malformed_yaml", path, "$", ex.Start, "Malformed YAML."); return false; }
    }

    private static void ScanMapping(IParser parser, string file, string path, List<ConfigDiagnostic> diagnostics, YamlMarks marks)
    {
        MappingStart start = (MappingStart)parser.Current!;
        if (!IsDefaultTag(start)) { Add(diagnostics, "unsupported_yaml_construct", file, path, start.Start, "Custom YAML tags are not supported."); throw new YamlException("Custom tag."); }
        marks.AddMapping(path, start.Start);
        parser.MoveNext();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        while (parser.Current is not MappingEnd)
        {
            if (parser.Current is not Scalar key) { Add(diagnostics, "wrong_type", file, path, MarkOf(parser.Current), "Mapping keys must be scalars."); throw new YamlException("Non-scalar key."); }
            if (!IsDefaultTag(key)) { Add(diagnostics, "unsupported_yaml_construct", file, path, key.Start, "Custom YAML tags are not supported."); throw new YamlException("Custom tag."); }
            string childPath = path + "." + key.Value;
            if (!keys.Add(key.Value)) Add(diagnostics, "duplicate_key", file, childPath, key.Start, "Duplicate keys are not allowed.");
            parser.MoveNext();
            ScanNode(parser, file, childPath, diagnostics, marks);
        }
        parser.MoveNext();
    }

    private static void ScanNode(IParser parser, string file, string path, List<ConfigDiagnostic> diagnostics, YamlMarks marks)
    {
        switch (parser.Current)
        {
            case MappingStart: ScanMapping(parser, file, path, diagnostics, marks); return;
            case Scalar scalar:
                if (!IsDefaultTag(scalar)) { Add(diagnostics, "unsupported_yaml_construct", file, path, scalar.Start, "Custom YAML tags are not supported."); throw new YamlException("Custom tag."); }
                marks.AddScalar(path, scalar); parser.MoveNext(); return;
            case SequenceStart sequence:
                Add(diagnostics, "wrong_type", file, path, sequence.Start, "Sequences are not supported in configuration."); throw new YamlException("Sequence.");
            case AnchorAlias alias:
                Add(diagnostics, "unsupported_yaml_construct", file, path, alias.Start, "YAML aliases are not supported."); throw new YamlException("Alias.");
            default:
                Add(diagnostics, "unsupported_yaml_construct", file, path, MarkOf(parser.Current), "Unsupported YAML construct."); throw new YamlException("Unsupported YAML.");
        }
    }

    private static bool ValidateScalarTypes(YamlMarks marks, string file, List<ConfigDiagnostic> diagnostics)
    {
        foreach ((string path, Scalar scalar) in marks.Scalars)
        {
            ScalarKind kind = path switch
            {
                "$.max_output_tokens" or "$.server_vad.prefix_padding_ms" or "$.server_vad.silence_duration_ms" => ScalarKind.Integer,
                "$.server_vad.threshold" => ScalarKind.Float,
                "$.app.verbose" => ScalarKind.Boolean,
                _ => ScalarKind.String,
            };
            bool valid = kind switch
            {
                ScalarKind.String => IsStringScalar(scalar),
                ScalarKind.Integer => scalar.Style == ScalarStyle.Plain && int.TryParse(scalar.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _),
                ScalarKind.Float => scalar.Style == ScalarStyle.Plain && float.TryParse(scalar.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) && float.IsFinite(value),
                ScalarKind.Boolean => scalar.Style == ScalarStyle.Plain && bool.TryParse(scalar.Value, out _),
                _ => false,
            };
            if (!valid)
            {
                string message = kind switch
                {
                    ScalarKind.String => "A string is required.",
                    ScalarKind.Integer => "An integer is required.",
                    ScalarKind.Float => "A finite number is required.",
                    ScalarKind.Boolean => "A boolean is required.",
                    _ => "Invalid scalar type.",
                };
                Add(diagnostics, "wrong_type", file, path, scalar.Start, message);
            }
        }
        return !HasErrors(diagnostics);
    }

    private static bool IsStringScalar(Scalar scalar)
    {
        if (scalar.Style != ScalarStyle.Plain) return true;
        string value = scalar.Value;
        if (value is "~" or "null" or "Null" or "NULL" or "true" or "True" or "TRUE" or "false" or "False" or "FALSE") return false;
        return !double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
    }

    private static string? RequiredString(string? value, List<ConfigDiagnostic> diagnostics, string file, string path, YamlMarks marks)
    {
        if (string.IsNullOrWhiteSpace(value)) { Add(diagnostics, "required_value", file, path, marks.At(path), "A non-empty value is required."); return null; }
        return value;
    }
    private static string? OptionalString(string? value, List<ConfigDiagnostic> diagnostics, string file, string path, YamlMarks marks)
    {
        if (value is null && !marks.Contains(path)) return null;
        return RequiredString(value, diagnostics, file, path, marks);
    }
    private static void Required(List<ConfigDiagnostic> diagnostics, string file, string path, YamlMarks marks) => Add(diagnostics, "required_value", file, path, marks.At(path), "A value is required.");
    private static bool HasErrors(List<ConfigDiagnostic> diagnostics) => diagnostics.Any(d => d.Severity == ConfigDiagnosticSeverity.Error);
    private static void Add(List<ConfigDiagnostic> list, string code, string? file, string path, Mark? mark, string message) => list.Add(new ConfigDiagnostic(code, file, path, mark is null ? null : checked((int)mark.Value.Line + 1), mark is null ? null : checked((int)mark.Value.Column + 1), message));
    private static Mark? MarkOf(ParsingEvent? e) => e?.Start;
    private static bool IsDefaultTag(NodeEvent node) => string.IsNullOrEmpty(node.Tag.ToString()) || node.Tag.ToString() == "?";

    private enum ScalarKind { String, Integer, Float, Boolean }

    private sealed class YamlMarks
    {
        private readonly Dictionary<string, Mark> _nodes = new(StringComparer.Ordinal);
        public Dictionary<string, Scalar> Scalars { get; } = new(StringComparer.Ordinal);
        public void AddMapping(string path, Mark mark) => _nodes[path] = mark;
        public void AddScalar(string path, Scalar scalar) { _nodes[path] = scalar.Start; Scalars[path] = scalar; }
        public bool Contains(string path) => _nodes.ContainsKey(path);
        public Mark? At(string path) => _nodes.TryGetValue(path, out Mark mark) ? mark : null;
        public string PathAt(Mark mark) => _nodes.FirstOrDefault(entry => entry.Value.Index == mark.Index).Key ?? "$";
    }
}
