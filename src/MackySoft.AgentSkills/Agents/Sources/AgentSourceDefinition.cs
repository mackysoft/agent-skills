namespace MackySoft.AgentSkills.Agents.Sources;

/// <summary> Represents one complete host-independent agent source definition. </summary>
internal sealed class AgentSourceDefinition
{
    /// <summary> Initializes an agent source definition. </summary>
    public AgentSourceDefinition (AgentSourceMetadata metadata, string instructionsTemplate, IReadOnlyList<AgentHostBindingSource> hostBindings)
    {
        Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        if (string.IsNullOrWhiteSpace(instructionsTemplate))
        {
            throw new ArgumentException("Agent instructions must not be empty.", nameof(instructionsTemplate));
        }

        ArgumentNullException.ThrowIfNull(hostBindings);
        var bindings = hostBindings.ToArray();
        if (bindings.Length == 0 || bindings.Any(static binding => binding is null) || bindings.GroupBy(static binding => binding.HostId).Any(static group => group.Count() != 1))
        {
            throw new ArgumentException("Agent host bindings must be non-empty and unique.", nameof(hostBindings));
        }

        InstructionsTemplate = instructionsTemplate;
        HostBindings = Array.AsReadOnly(bindings.OrderBy(static binding => Vocabulary.GetText(binding.HostId), StringComparer.Ordinal).ToArray());
    }

    /// <summary> Gets host-independent agent metadata. </summary>
    public AgentSourceMetadata Metadata { get; }

    /// <summary> Gets normalized host-independent instructions. </summary>
    public string InstructionsTemplate { get; }

    /// <summary> Gets validated host bindings. </summary>
    public IReadOnlyList<AgentHostBindingSource> HostBindings { get; }
}
