using MackySoft.AgentSkills.Packaging.Canonical;

namespace MackySoft.AgentSkills.Bundles;

/// <summary> Holds a descriptor and canonical packages before bundle-level relational validation. </summary>
internal sealed class CanonicalSkillBundleCandidate
{
    internal CanonicalSkillBundleCandidate (
        SkillBundleDescriptor descriptor,
        IReadOnlyList<CanonicalSkillPackage> packages)
    {
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        ArgumentNullException.ThrowIfNull(packages);
        if (packages.Any(static package => package is null))
        {
            throw new ArgumentException("Canonical SKILL bundle candidate must not contain null packages.", nameof(packages));
        }

        Packages = Array.AsReadOnly(packages.ToArray());
    }

    internal SkillBundleDescriptor Descriptor { get; }

    internal IReadOnlyList<CanonicalSkillPackage> Packages { get; }
}
