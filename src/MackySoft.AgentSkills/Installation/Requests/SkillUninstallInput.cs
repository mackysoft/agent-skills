using MackySoft.AgentSkills.Installation.Targeting;
using MackySoft.AgentSkills.Packaging.Canonical;

namespace MackySoft.AgentSkills.Installation.Requests;

/// <summary> Represents one SKILL uninstall service input. </summary>
/// <param name="Packages"> The canonical packages to remove. </param>
/// <param name="TargetRequest"> The host target request. </param>
/// <param name="DryRun"> Whether to return a plan without writing to the file system. </param>
/// <param name="Force"> Whether managed local modifications can be deleted. </param>
public sealed record SkillUninstallInput (
    IReadOnlyList<CanonicalSkillPackage> Packages,
    SkillInstallRequest TargetRequest,
    bool DryRun = false,
    bool Force = false);
