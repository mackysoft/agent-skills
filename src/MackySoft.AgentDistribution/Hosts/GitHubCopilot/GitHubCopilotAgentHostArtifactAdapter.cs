using System.Text.Json.Serialization;
using MackySoft.AgentDistribution.Agents.Sources;
using MackySoft.AgentDistribution.Hosts.Serialization;
using MackySoft.AgentDistribution.Serialization.Yaml;
using MackySoft.AgentDistribution.Shared;

namespace MackySoft.AgentDistribution.Hosts.GitHubCopilot;

/// <summary>Validates GitHub Copilot bindings and generates custom-agent Markdown files.</summary>
internal sealed class GitHubCopilotAgentHostArtifactAdapter : IAgentHostArtifactAdapter
{
    private static readonly IReadOnlySet<string> Targets = new HashSet<string>(
        ["vscode", "github-copilot"],
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

        var bindingResult = ParseBinding(bindingJson);
        if (!bindingResult.IsSuccess)
        {
            throw new ArgumentException(bindingResult.Failure!.Message, nameof(bindingJson));
        }

        var binding = bindingResult.Value!;
        var yaml = new DeterministicYamlBuilder()
            .DocumentMarker()
            .Mapping("description", metadata.Description);

        if (binding.Target is not null)
        {
            yaml.Mapping("target", binding.Target);
        }

        if (binding.Tools is not null)
        {
            yaml.Sequence("tools", binding.Tools);
        }

        if (binding.Model is not null)
        {
            yaml.Mapping("model", binding.Model);
        }

        if (binding.DisableModelInvocation is not null)
        {
            yaml.Mapping("disable-model-invocation", binding.DisableModelInvocation.Value);
        }

        if (binding.UserInvocable is not null)
        {
            yaml.Mapping("user-invocable", binding.UserInvocable.Value);
        }

        var content = yaml
            .DocumentMarker()
            .BlankLine()
            .Build()
            + EnsureTrailingLineFeed(agentInstructions);

        return new AgentHostArtifactSet(
            [new AgentHostArtifactFile(PackageRelativePath.Parse($"{metadata.AgentName.Value}.agent.md"), content)]);
    }

    private static SkillOperationResult<GitHubCopilotBinding> ParseBinding (string bindingJson)
    {
        var deserialized = AgentHostBindingJson.Deserialize<GitHubCopilotBinding>(bindingJson, "GitHub Copilot");
        if (!deserialized.IsSuccess)
        {
            return deserialized;
        }

        var binding = deserialized.Value!;
        if (binding.SchemaVersion != 1)
        {
            return Failure("GitHub Copilot agent binding schemaVersion must be 1.");
        }

        if (binding.Target is not null && !Targets.Contains(binding.Target))
        {
            return Failure("GitHub Copilot agent binding target is not supported.");
        }

        if (!IsStringSetValid(binding.Tools))
        {
            return Failure("GitHub Copilot agent binding tools must contain unique, non-empty tool names.");
        }

        if (!IsOptionalTextValid(binding.Model))
        {
            return Failure("GitHub Copilot agent binding model must be a non-empty string without control characters.");
        }

        return SkillOperationResult<GitHubCopilotBinding>.Success(binding);
    }

    private static bool IsStringSetValid (IReadOnlyList<string>? values)
    {
        if (values is null)
        {
            return true;
        }

        return values.All(static value =>
                !string.IsNullOrWhiteSpace(value)
                && value.Length <= 256
                && !value.Any(char.IsControl))
            && values.Distinct(StringComparer.Ordinal).Count() == values.Count;
    }

    private static bool IsOptionalTextValid (string? value)
    {
        return value is null
            || (!string.IsNullOrWhiteSpace(value)
                && value.Length <= 256
                && !value.Any(char.IsControl));
    }

    private static string EnsureTrailingLineFeed (string instructions)
    {
        var normalized = SkillTextNormalizer.NormalizeToLf(instructions);
        return normalized.EndsWith('\n') ? normalized : normalized + '\n';
    }

    private static SkillOperationResult<GitHubCopilotBinding> Failure (string message)
    {
        return SkillOperationResult<GitHubCopilotBinding>.FailureResult(SkillFailureCodes.SourceInvalid, message);
    }

    private sealed class GitHubCopilotBinding
    {
        [JsonPropertyName("schemaVersion")]
        public int SchemaVersion { get; init; }

        [JsonPropertyName("target")]
        public string? Target { get; init; }

        [JsonPropertyName("tools")]
        public string[]? Tools { get; init; }

        [JsonPropertyName("model")]
        public string? Model { get; init; }

        [JsonPropertyName("disableModelInvocation")]
        public bool? DisableModelInvocation { get; init; }

        [JsonPropertyName("userInvocable")]
        public bool? UserInvocable { get; init; }
    }
}
