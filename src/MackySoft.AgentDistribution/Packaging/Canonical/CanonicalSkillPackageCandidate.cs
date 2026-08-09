using MackySoft.AgentDistribution.Manifests;
using MackySoft.AgentDistribution.Shared;

namespace MackySoft.AgentDistribution.Packaging.Canonical;

/// <summary> Holds a canonical manifest and package files before their relational validation. </summary>
internal sealed class CanonicalSkillPackageCandidate
{
    internal CanonicalSkillPackageCandidate (
        SkillManifest manifest,
        IReadOnlyList<PackageTextFile> files)
    {
        Manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
        ArgumentNullException.ThrowIfNull(files);
        if (files.Any(static file => file is null))
        {
            throw new ArgumentException("Canonical SKILL package candidate must not contain null files.", nameof(files));
        }

        Files = Array.AsReadOnly(files.ToArray());
    }

    internal SkillManifest Manifest { get; }

    internal IReadOnlyList<PackageTextFile> Files { get; }
}
