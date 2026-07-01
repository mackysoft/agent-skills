using MackySoft.AgentSkills.Names;

namespace MackySoft.AgentSkills.Hosts.Contracts;

/// <summary> Represents host-independent metadata needed to build host artifacts. </summary>
/// <param name="SkillName"> The skill name. </param>
/// <param name="DisplayName"> The display name. </param>
/// <param name="Description"> The skill description. </param>
public sealed record SkillHostMetadata (
    SkillName SkillName,
    string DisplayName,
    string Description);
