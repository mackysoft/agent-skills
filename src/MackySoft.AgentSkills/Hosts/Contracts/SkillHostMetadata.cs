using MackySoft.AgentSkills.Names;

namespace MackySoft.AgentSkills.Hosts.Contracts;

/// <summary> Represents host-independent metadata needed to build host artifacts. </summary>
internal sealed class SkillHostMetadata
{
    /// <summary> Initializes one immutable host metadata value. </summary>
    public SkillHostMetadata (
        SkillName skillName,
        string displayName,
        string description)
    {
        ArgumentNullException.ThrowIfNull(skillName);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        SkillName = skillName;
        DisplayName = displayName;
        Description = description;
    }

    /// <summary> Gets the skill name. </summary>
    public SkillName SkillName { get; }

    /// <summary> Gets the display name. </summary>
    public string DisplayName { get; }

    /// <summary> Gets the skill description. </summary>
    public string Description { get; }
}
