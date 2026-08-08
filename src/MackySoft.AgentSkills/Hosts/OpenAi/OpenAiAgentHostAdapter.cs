using System.Text;
using System.Text.Json;
using MackySoft.AgentSkills.Agents.Hosts;
using MackySoft.AgentSkills.Agents.Sources;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Hosts.OpenAi;

/// <summary> Validates and materializes OpenAI / Codex custom-agent bindings. </summary>
internal sealed class OpenAiAgentHostAdapter : IAgentHostAdapter
{
    private static readonly string[] ExpectedBindingProperties =
    [
        "schemaVersion",
        "modelProvider",
        "model",
        "reasoningEffort",
        "verbosity",
        "sandboxMode",
        "features",
        "overridesBuiltIn",
    ];

    private const string OpenAiModelProvider = "openai";

    /// <inheritdoc />
    public AgentHostDescriptor Descriptor { get; } = new(
        AgentHostKind.OpenAi,
        ".codex/agents",
        ".codex/agent-skills/agents",
        new AgentUserTargetRootPolicy(
            "CODEX_HOME",
            "agents",
            "agent-skills/agents",
            ".codex/agents",
            ".codex/agent-skills/agents"),
        ".agent-skills");

    /// <inheritdoc />
    public AgentHostKind HostId => AgentHostKind.OpenAi;

    /// <inheritdoc />
    public SkillOperationResult<bool> ValidateBinding (string bindingJson)
    {
        var bindingResult = ParseBinding(bindingJson);
        return bindingResult.IsSuccess
            ? SkillOperationResult<bool>.Success(true)
            : SkillOperationResult<bool>.FailureResult(bindingResult.Failure!.Code, bindingResult.Failure.Message);
    }

    /// <inheritdoc />
    public AgentHostArtifactSet BuildArtifacts (
        AgentSourceMetadata metadata,
        string agentInstructions,
        string bindingJson)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(agentInstructions);

        var bindingResult = ParseBinding(bindingJson);
        if (!bindingResult.IsSuccess)
        {
            throw new ArgumentException(bindingResult.Failure!.Message, nameof(bindingJson));
        }

        var binding = bindingResult.Value!;
        ValidateBuiltInOverride(metadata.AgentName.Value, binding.OverridesBuiltIn);
        var content = BuildToml(metadata, agentInstructions, binding);
        return new AgentHostArtifactSet([new AgentHostArtifactFile($"{metadata.AgentName.Value}.toml", content)]);
    }

    private static SkillOperationResult<OpenAiAgentBinding> ParseBinding (string bindingJson)
    {
        if (string.IsNullOrWhiteSpace(bindingJson))
        {
            return Failure("OpenAI agent binding must not be empty.");
        }

        try
        {
            using var document = JsonDocument.Parse(bindingJson);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return Failure("OpenAI agent binding must be a JSON object.");
            }

            var properties = root.EnumerateObject().Select(static property => property.Name).ToArray();
            if (!ExpectedBindingProperties.SequenceEqual(properties, StringComparer.Ordinal))
            {
                return Failure("OpenAI agent binding must contain only schemaVersion, modelProvider, model, reasoningEffort, verbosity, sandboxMode, features, and overridesBuiltIn in canonical order.");
            }

            var schemaVersion = ReadInt32(root, "schemaVersion");
            if (schemaVersion != 1)
            {
                return Failure("OpenAI agent binding schemaVersion must be 1.");
            }

            var modelProvider = ReadNonWhitespaceString(root, "modelProvider");
            if (!string.Equals(modelProvider, OpenAiModelProvider, StringComparison.Ordinal))
            {
                return Failure("OpenAI agent binding modelProvider must be openai.");
            }

            var model = ReadModelLiteral(root, "model");
            var reasoningEffort = ReadLiteral(root, "reasoningEffort", ["high", "max"]);
            var verbosity = ReadLiteral(root, "verbosity", ["low"]);
            var sandboxMode = ReadLiteral(root, "sandboxMode", ["read-only", "workspace-write"]);
            var multiAgent = ReadMultiAgent(root);
            var overridesBuiltIn = ReadBoolean(root, "overridesBuiltIn");

            return SkillOperationResult<OpenAiAgentBinding>.Success(
                new OpenAiAgentBinding(model, reasoningEffort, verbosity, sandboxMode, multiAgent, overridesBuiltIn));
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException)
        {
            return Failure("OpenAI agent binding contains an invalid value.");
        }
    }

    private static int ReadInt32 (JsonElement root, string propertyName)
    {
        var value = root.GetProperty(propertyName);
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var result))
        {
            throw new FormatException($"OpenAI agent binding {propertyName} must be an integer.");
        }

        return result;
    }

    private static string ReadNonWhitespaceString (JsonElement root, string propertyName)
    {
        var value = root.GetProperty(propertyName);
        if (value.ValueKind != JsonValueKind.String)
        {
            throw new FormatException($"OpenAI agent binding {propertyName} must be a string.");
        }

        var result = value.GetString();
        if (string.IsNullOrWhiteSpace(result))
        {
            throw new FormatException($"OpenAI agent binding {propertyName} must not be empty or whitespace.");
        }

        return result;
    }

    private static string ReadModelLiteral (JsonElement root, string propertyName)
    {
        var value = ReadNonWhitespaceString(root, propertyName);
        if (!IsSafeModelLiteral(value))
        {
            throw new FormatException("OpenAI agent binding model must be a safe model literal.");
        }

        return value;
    }

    private static bool IsSafeModelLiteral (string value)
    {
        if (value.Length > 128)
        {
            return false;
        }

        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (character is (>= 'a' and <= 'z') or (>= '0' and <= '9'))
            {
                continue;
            }

            if (character is not '.' and not '-'
                || index == 0
                || index == value.Length - 1
                || value[index - 1] is not ((>= 'a' and <= 'z') or (>= '0' and <= '9'))
                || value[index + 1] is not ((>= 'a' and <= 'z') or (>= '0' and <= '9')))
            {
                return false;
            }
        }

        return true;
    }

    private static string ReadLiteral (JsonElement root, string propertyName, IReadOnlyList<string> allowedValues)
    {
        var value = ReadNonWhitespaceString(root, propertyName);
        if (!allowedValues.Contains(value, StringComparer.Ordinal))
        {
            throw new FormatException($"OpenAI agent binding {propertyName} is not supported.");
        }

        return value;
    }

    private static bool ReadMultiAgent (JsonElement root)
    {
        var features = root.GetProperty("features");
        if (features.ValueKind != JsonValueKind.Object)
        {
            throw new FormatException("OpenAI agent binding features must be an object.");
        }

        var properties = features.EnumerateObject().Select(static property => property.Name).ToArray();
        if (!properties.SequenceEqual(["multiAgent"], StringComparer.Ordinal))
        {
            throw new FormatException("OpenAI agent binding features must contain only multiAgent.");
        }

        return ReadBoolean(features, "multiAgent");
    }

    private static bool ReadBoolean (JsonElement root, string propertyName)
    {
        var value = root.GetProperty(propertyName);
        if (value.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
        {
            throw new FormatException($"OpenAI agent binding {propertyName} must be a boolean.");
        }

        return value.GetBoolean();
    }

    private static void ValidateBuiltInOverride (string agentName, bool overridesBuiltIn)
    {
        var requiresOverride = agentName is "worker" or "explorer";
        if (requiresOverride != overridesBuiltIn)
        {
            throw new ArgumentException(
                requiresOverride
                    ? $"OpenAI built-in agent '{agentName}' requires overridesBuiltIn=true."
                    : "OpenAI overridesBuiltIn=true is supported only for worker and explorer.",
                nameof(overridesBuiltIn));
        }
    }

    private static string BuildToml (AgentSourceMetadata metadata, string instructions, OpenAiAgentBinding binding)
    {
        var builder = new StringBuilder();
        AppendString(builder, "name", metadata.AgentName.Value);
        AppendString(builder, "description", metadata.Description);
        AppendString(builder, "model_provider", OpenAiModelProvider);
        AppendString(builder, "model", binding.Model);
        AppendString(builder, "model_reasoning_effort", binding.ReasoningEffort);
        AppendString(builder, "model_verbosity", binding.Verbosity);
        AppendString(builder, "sandbox_mode", binding.SandboxMode);
        AppendString(builder, "developer_instructions", SkillTextNormalizer.NormalizeToLf(instructions));
        builder.Append('\n');
        builder.Append("[features]\n");
        builder.Append("multi_agent = ");
        builder.Append(binding.MultiAgent ? "true\n" : "false\n");
        return builder.ToString();
    }

    private static void AppendString (StringBuilder builder, string key, string value)
    {
        builder.Append(key);
        builder.Append(" = \"");
        foreach (var character in value)
        {
            switch (character)
            {
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\b':
                    builder.Append("\\b");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\f':
                    builder.Append("\\f");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                default:
                    builder.Append(character);
                    break;
            }
        }

        builder.Append("\"\n");
    }

    private static SkillOperationResult<OpenAiAgentBinding> Failure (string message)
    {
        return SkillOperationResult<OpenAiAgentBinding>.FailureResult(SkillFailureCodes.SourceInvalid, message);
    }

    private sealed class OpenAiAgentBinding
    {
        public OpenAiAgentBinding (string model, string reasoningEffort, string verbosity, string sandboxMode, bool multiAgent, bool overridesBuiltIn)
        {
            Model = model;
            ReasoningEffort = reasoningEffort;
            Verbosity = verbosity;
            SandboxMode = sandboxMode;
            MultiAgent = multiAgent;
            OverridesBuiltIn = overridesBuiltIn;
        }

        public string Model { get; }

        public string ReasoningEffort { get; }

        public string Verbosity { get; }

        public string SandboxMode { get; }

        public bool MultiAgent { get; }

        public bool OverridesBuiltIn { get; }
    }
}
