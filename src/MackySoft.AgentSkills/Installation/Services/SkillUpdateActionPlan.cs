using MackySoft.AgentSkills.Installation.Diffing;
using MackySoft.AgentSkills.Installation.Results;
using MackySoft.AgentSkills.Materialization;
using MackySoft.AgentSkills.Packaging.Canonical;
using MackySoft.FileSystem;

namespace MackySoft.AgentSkills.Installation.Services;

internal sealed class SkillUpdateActionPlan
{
    public SkillUpdateActionPlan (
        SkillUpdateAction action,
        AbsolutePath skillDirectory,
        CanonicalSkillPackage package,
        SkillMaterializedPackage? materializedPackage,
        SkillActionTargetSnapshot? targetSnapshot)
    {
        Action = action ?? throw new ArgumentNullException(nameof(action));
        ArgumentNullException.ThrowIfNull(skillDirectory);
        if ((materializedPackage is null) != (targetSnapshot is null))
        {
            throw new ArgumentException("A materialized package and its target snapshot must be provided together.", nameof(targetSnapshot));
        }

        SkillDirectory = skillDirectory;
        Package = package ?? throw new ArgumentNullException(nameof(package));
        MaterializedPackage = materializedPackage;
        TargetSnapshot = targetSnapshot;
    }

    public SkillUpdateAction Action { get; }

    public AbsolutePath SkillDirectory { get; }

    public CanonicalSkillPackage Package { get; }

    public SkillMaterializedPackage? MaterializedPackage { get; }

    public SkillActionTargetSnapshot? TargetSnapshot { get; }
}
