using System.Text.Json.Serialization;
using MackySoft.AgentDistribution.Agents.Sources;
using MackySoft.AgentDistribution.Hosts.Serialization;
using MackySoft.AgentDistribution.Serialization.Yaml;
using MackySoft.AgentDistribution.Shared;

namespace MackySoft.AgentDistribution.Hosts.ClaudeCode;

/// <summary>Validates Claude Code bindings and generates subagent Markdown files.</summary>
internal sealed class ClaudeCodeAgentHostArtifactAdapter : IAgentHostArtifactAdapter
{
    private static readonly IReadOnlySet<string> PermissionModes = new HashSet<string>(
        ["default", "manual", "acceptEdits", "auto", "dontAsk", "bypassPermissions", "plan"],
        StringComparer.Ordinal);

    /// <inheritdoc />
    public AgentDistributionOperationResult<bool> ValidateBinding (string bindingJson)
    {
        var binding = ParseBinding(bindingJson);
        return binding.IsSuccess
            ? AgentDistributionOperationResult<bool>.Success(true)
            : AgentDistributionOperationResult<bool>.FailureResult(binding.Failure!.Code, binding.Failure.Message);
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
        var yaml = new DeterministicYamlBuilder()
            .DocumentMarker()
            .Mapping("name", metadata.AgentName.Value)
            .Mapping("description", metadata.Description);

        if (binding.Tools is not null)
        {
            yaml.Sequence("tools", binding.Tools);
        }

        if (binding.DisallowedTools is not null)
        {
            yaml.Sequence("disallowedTools", binding.DisallowedTools);
        }

        if (binding.Model is not null)
        {
            yaml.Mapping("model", binding.Model);
        }

        if (binding.PermissionMode is not null)
        {
            yaml.Mapping("permissionMode", binding.PermissionMode);
        }

        if (binding.MaxTurns is not null)
        {
            yaml.Mapping("maxTurns", binding.MaxTurns.Value);
        }

        var content = yaml
            .DocumentMarker()
            .BlankLine()
            .Build()
            + EnsureTrailingLineFeed(agentInstructions);

        return new AgentHostArtifactSet(
            [new AgentHostArtifactFile(PackageRelativePath.Parse($"{metadata.AgentName.Value}.md"), content)]);
    }

    private static AgentDistributionOperationResult<ClaudeCodeBinding> ParseBinding (string bindingJson)
    {
        var deserialized = AgentHostBindingJson.Deserialize<ClaudeCodeBinding>(bindingJson, "Claude Code");
        if (!deserialized.IsSuccess)
        {
            return deserialized;
        }

        var binding = deserialized.Value!;
        if (binding.SchemaVersion != 1)
        {
            return Failure("Claude Code agent binding schemaVersion must be 1.");
        }

        if (!IsOptionalTextValid(binding.Model))
        {
            return Failure("Claude Code agent binding model must be a non-empty string without control characters.");
        }

        if (!IsStringSetValid(binding.Tools))
        {
            return Failure("Claude Code agent binding tools must be a non-empty set of non-empty tool names.");
        }

        if (!IsStringSetValid(binding.DisallowedTools))
        {
            return Failure("Claude Code agent binding disallowedTools must be a non-empty set of non-empty tool names.");
        }

        if (binding.PermissionMode is not null && !PermissionModes.Contains(binding.PermissionMode))
        {
            return Failure("Claude Code agent binding permissionMode is not supported.");
        }

        if (binding.MaxTurns is <= 0)
        {
            return Failure("Claude Code agent binding maxTurns must be greater than zero.");
        }

        return AgentDistributionOperationResult<ClaudeCodeBinding>.Success(binding);
    }

    private static bool IsOptionalTextValid (string? value)
    {
        return value is null
            || (!string.IsNullOrWhiteSpace(value)
                && value.Length <= 256
                && !value.Any(char.IsControl));
    }

    private static bool IsStringSetValid (IReadOnlyList<string>? values)
    {
        if (values is null)
        {
            return true;
        }

        return values.Count > 0
            && values.All(static value =>
                !string.IsNullOrWhiteSpace(value)
                && value.Length <= 256
                && !value.Any(char.IsControl))
            && values.Distinct(StringComparer.Ordinal).Count() == values.Count;
    }

    private static string EnsureTrailingLineFeed (string instructions)
    {
        var normalized = AgentDistributionTextNormalizer.NormalizeToLf(instructions);
        return normalized.EndsWith('\n') ? normalized : normalized + '\n';
    }

    private static AgentDistributionOperationResult<ClaudeCodeBinding> Failure (string message)
    {
        return AgentDistributionOperationResult<ClaudeCodeBinding>.FailureResult(AgentDistributionFailureCodes.SourceInvalid, message);
    }

    private sealed class ClaudeCodeBinding
    {
        [JsonPropertyName("schemaVersion")]
        public int SchemaVersion { get; init; }

        [JsonPropertyName("model")]
        public string? Model { get; init; }

        [JsonPropertyName("tools")]
        public string[]? Tools { get; init; }

        [JsonPropertyName("disallowedTools")]
        public string[]? DisallowedTools { get; init; }

        [JsonPropertyName("permissionMode")]
        public string? PermissionMode { get; init; }

        [JsonPropertyName("maxTurns")]
        public int? MaxTurns { get; init; }
    }
}
