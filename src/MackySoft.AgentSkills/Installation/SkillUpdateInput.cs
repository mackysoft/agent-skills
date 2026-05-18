using MackySoft.AgentSkills.Packaging;

namespace MackySoft.AgentSkills.Installation;

/// <summary> Represents one SKILL update service input. </summary>
/// <param name="Packages"> The canonical packages to reconcile. </param>
/// <param name="TargetRequest"> The host target request. </param>
/// <param name="DryRun"> Whether to return a plan without writing to the file system. </param>
/// <param name="Force"> Whether managed local modifications can be overwritten. </param>
/// <param name="PrintDiff"> Whether per-file diff payloads should be included. </param>
public sealed record SkillUpdateInput (
    IReadOnlyList<CanonicalSkillPackage> Packages,
    SkillInstallRequest TargetRequest,
    bool DryRun = false,
    bool Force = false,
    bool PrintDiff = false);
