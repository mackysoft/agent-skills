using System.Text;
using System.Text.Json.Serialization;
using MackySoft.AgentDistribution.Agents.Sources;
using MackySoft.AgentDistribution.Hosts.Serialization;
using MackySoft.AgentDistribution.Shared;

namespace MackySoft.AgentDistribution.Hosts.Codex;

/// <summary>Validates Codex bindings and generates standalone custom-agent TOML files.</summary>
internal sealed class CodexAgentHostArtifactAdapter : IAgentHostArtifactAdapter
{
    private static readonly IReadOnlySet<AgentName> BuiltInAgentNames = new HashSet<AgentName>(
        [new("default"), new("worker"), new("explorer")]);

    private static readonly IReadOnlySet<string> SandboxModes = new HashSet<string>(
        ["read-only", "workspace-write", "danger-full-access"],
        StringComparer.Ordinal);

    /// <inheritdoc />
    public SkillOperationResult<bool> ValidateBinding (string bindingJson)
    {
        var binding = ParseBinding(bindingJson);
        return binding.IsSuccess
            ? SkillOperationResult<bool>.Success(true)
            : SkillOperationResult<bool>.FailureResult(binding.Failure!.Code, binding.Failure.Message);
    }

    /// <inheritdoc />
    public AgentHostArtifactSet BuildArtifacts (
        AgentSourceMetadata metadata,
        string agentInstructions,
        string bindingJson)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(agentInstructions);
        RejectBuiltInAgentName(metadata.AgentName);

        var bindingResult = ParseBinding(bindingJson);
        if (!bindingResult.IsSuccess)
        {
            throw new ArgumentException(bindingResult.Failure!.Message, nameof(bindingJson));
        }

        var binding = bindingResult.Value!;
        var builder = new StringBuilder();
        AppendString(builder, "name", metadata.AgentName.Value);
        AppendString(builder, "description", metadata.Description);
        AppendOptionalString(builder, "model", binding.Model);
        AppendOptionalString(builder, "model_reasoning_effort", binding.ReasoningEffort);
        AppendOptionalString(builder, "sandbox_mode", binding.SandboxMode);
        AppendString(builder, "developer_instructions", SkillTextNormalizer.NormalizeToLf(agentInstructions));

        return new AgentHostArtifactSet(
            [new AgentHostArtifactFile(PackageRelativePath.Parse($"{metadata.AgentName.Value}.toml"), builder.ToString())]);
    }

    private static SkillOperationResult<CodexBinding> ParseBinding (string bindingJson)
    {
        var deserialized = AgentHostBindingJson.Deserialize<CodexBinding>(bindingJson, "Codex");
        if (!deserialized.IsSuccess)
        {
            return deserialized;
        }

        var binding = deserialized.Value!;
        if (binding.SchemaVersion != 1)
        {
            return Failure("Codex agent binding schemaVersion must be 1.");
        }

        if (!IsOptionalTextValid(binding.Model))
        {
            return Failure("Codex agent binding model must be a non-empty string without control characters.");
        }

        if (!IsOptionalTextValid(binding.ReasoningEffort))
        {
            return Failure("Codex agent binding reasoningEffort must be a non-empty string without control characters.");
        }

        if (binding.SandboxMode is not null && !SandboxModes.Contains(binding.SandboxMode))
        {
            return Failure("Codex agent binding sandboxMode is not supported.");
        }

        return SkillOperationResult<CodexBinding>.Success(binding);
    }

    private static bool IsOptionalTextValid (string? value)
    {
        if (value is null)
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(value)
            && value.Length <= 256
            && !value.Any(char.IsControl);
    }

    private static void RejectBuiltInAgentName (AgentName agentName)
    {
        if (BuiltInAgentNames.Contains(agentName))
        {
            throw new ArgumentException(
                $"Codex agent name '{agentName}' is reserved by a built-in agent.",
                nameof(agentName));
        }
    }

    private static void AppendOptionalString (StringBuilder builder, string key, string? value)
    {
        if (value is not null)
        {
            AppendString(builder, key, value);
        }
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
                    if (char.IsControl(character))
                    {
                        builder.Append($"\\u{(int)character:X4}");
                    }
                    else
                    {
                        builder.Append(character);
                    }
                    break;
            }
        }

        builder.Append("\"\n");
    }

    private static SkillOperationResult<CodexBinding> Failure (string message)
    {
        return SkillOperationResult<CodexBinding>.FailureResult(SkillFailureCodes.SourceInvalid, message);
    }

    private sealed class CodexBinding
    {
        [JsonPropertyName("schemaVersion")]
        public int SchemaVersion { get; init; }

        [JsonPropertyName("model")]
        public string? Model { get; init; }

        [JsonPropertyName("reasoningEffort")]
        public string? ReasoningEffort { get; init; }

        [JsonPropertyName("sandboxMode")]
        public string? SandboxMode { get; init; }
    }
}
