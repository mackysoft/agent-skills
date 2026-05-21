using MackySoft.AgentSkills.Installation.Targeting;
using MackySoft.AgentSkills.Packaging.Canonical;

namespace MackySoft.AgentSkills.Installation.Requests;

/// <summary> Represents one SKILL update service input. </summary>
/// <param name="Packages"> The canonical packages to reconcile. </param>
/// <param name="TargetRequest"> The host target request. </param>
/// <param name="DryRun"> Whether to return a plan without writing to the file system. </param>
/// <param name="Force">
/// Whether managed locally modified targets can be replaced. Clean-outdated targets can be replaced without force;
/// force does not allow replacing unmanaged targets, name collisions, host conflicts, or path-safety failures.
/// </param>
/// <param name="PrintDiff"> Whether per-file diff payloads should be included. </param>
public sealed record SkillUpdateInput (
    IReadOnlyList<CanonicalSkillPackage> Packages,
    SkillInstallRequest TargetRequest,
    bool DryRun = false,
    bool Force = false,
    bool PrintDiff = false);
