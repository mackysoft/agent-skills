namespace MackySoft.AgentDistribution.Agents.Sources;

/// <summary> Represents host-independent metadata assembled from one agent source directory. </summary>
internal sealed class AgentSourceMetadata
{
    /// <summary> Gets the only supported agent source schema version. </summary>
    internal const int CurrentSchemaVersion = 1;

    /// <summary> Initializes agent metadata. </summary>
    public AgentSourceMetadata (int schemaVersion, AgentCategory category, AgentName agentName, string displayName, string description, IReadOnlyList<SkillName> skillDependencies)
    {
        if (schemaVersion != CurrentSchemaVersion)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion), schemaVersion, $"Agent source schema version must be {CurrentSchemaVersion}.");
        }

        ArgumentNullException.ThrowIfNull(category);
        ArgumentNullException.ThrowIfNull(agentName);
        if (string.IsNullOrWhiteSpace(displayName)
            || string.IsNullOrWhiteSpace(description)
            || displayName.Any(char.IsControl)
            || description.Any(char.IsControl)
            || description.Length > 1024)
        {
            throw new ArgumentException("Agent display name and description must be present, must not contain control characters, and description must not exceed 1024 characters.");
        }

        ArgumentNullException.ThrowIfNull(skillDependencies);
        var dependencies = skillDependencies.ToArray();
        if (dependencies.Any(static dependency => dependency is null) || dependencies.Distinct().Count() != dependencies.Length)
        {
            throw new ArgumentException("Agent dependencies on skills must be unique and non-null.", nameof(skillDependencies));
        }

        SchemaVersion = schemaVersion;
        Category = category;
        AgentName = agentName;
        DisplayName = displayName;
        Description = description;
        SkillDependencies = Array.AsReadOnly(dependencies.OrderBy(static dependency => dependency.Value, StringComparer.Ordinal).ToArray());
    }

    /// <summary> Gets the source schema version. </summary>
    public int SchemaVersion { get; }

    /// <summary> Gets the directory-derived category. </summary>
    public AgentCategory Category { get; }

    /// <summary> Gets the directory-derived agent name. </summary>
    public AgentName AgentName { get; }

    /// <summary> Gets the human-readable display name. </summary>
    public string DisplayName { get; }

    /// <summary> Gets the host-independent description. </summary>
    public string Description { get; }

    /// <summary> Gets direct skill dependencies. </summary>
    public IReadOnlyList<SkillName> SkillDependencies { get; }
}
