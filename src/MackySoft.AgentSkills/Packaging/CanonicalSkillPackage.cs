using MackySoft.AgentSkills.Manifests;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Packaging;

/// <summary> Represents one canonical host-independent SKILL package. </summary>
/// <param name="Manifest"> The canonical manifest. </param>
/// <param name="Files"> The canonical package files. </param>
public sealed record CanonicalSkillPackage (
    SkillManifest Manifest,
    IReadOnlyList<SkillPackageFile> Files);
