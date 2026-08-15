using MackySoft.AgentDistribution.Installation.Diffing;
using MackySoft.AgentDistribution.Installation.Results;
using MackySoft.AgentDistribution.Materialization;
using MackySoft.AgentDistribution.Skills.Packaging.Canonical;
using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Installation.Services;

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
