using MackySoft.AgentSkills.Names;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Materialization;

/// <summary> Represents a host-materialized SKILL package. </summary>
/// <param name="SkillName"> The skill name. </param>
/// <param name="Host"> The host. </param>
/// <param name="Files"> The materialized package files. </param>
public sealed record SkillMaterializedPackage (
    SkillName SkillName,
    string Host,
    IReadOnlyList<SkillPackageFile> Files);
