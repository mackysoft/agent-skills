using MackySoft.AgentDistribution.Shared;

namespace MackySoft.AgentDistribution.Hosts.Contracts;

/// <summary> Represents the host-owned input required to generate one agent artifact. </summary>
internal sealed class AgentHostArtifactRequest
{
    /// <summary> Initializes one immutable host artifact request. </summary>
    public AgentHostArtifactRequest (
        AgentName agentName,
        string description,
        string normalizedInstructions,
        string bindingJson)
    {
        ArgumentNullException.ThrowIfNull(agentName);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(normalizedInstructions);
        ArgumentNullException.ThrowIfNull(bindingJson);
        if (!string.Equals(normalizedInstructions, AgentDistributionTextNormalizer.NormalizeToLf(normalizedInstructions), StringComparison.Ordinal))
        {
            throw new ArgumentException("Agent instructions must be normalized to LF.", nameof(normalizedInstructions));
        }

        AgentName = agentName;
        Description = description;
        NormalizedInstructions = normalizedInstructions;
        BindingJson = bindingJson;
    }

    /// <summary> Gets the directory-derived agent name. </summary>
    public AgentName AgentName { get; }

    /// <summary> Gets the host-independent agent description. </summary>
    public string Description { get; }

    /// <summary> Gets the LF-normalized agent instructions. </summary>
    public string NormalizedInstructions { get; }

    /// <summary> Gets the validated host-owned binding JSON. </summary>
    public string BindingJson { get; }
}
