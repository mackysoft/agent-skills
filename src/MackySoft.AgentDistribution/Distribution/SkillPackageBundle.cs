using MackySoft.AgentDistribution.Skills.Bundles;
using MackySoft.AgentDistribution.Skills.Packaging.Canonical;

namespace MackySoft.AgentDistribution.Distribution;

/// <summary> Represents the SKILL-only view used by distribution operations across generated bundle schemas. </summary>
internal sealed class SkillPackageBundle
{
    /// <summary> Initializes one validated SKILL package view. </summary>
    /// <param name="descriptor"> The SKILL descriptor projected from the generated bundle. </param>
    /// <param name="packages"> The complete canonical SKILL package set. </param>
    public SkillPackageBundle (
        SkillBundleDescriptor descriptor,
        IReadOnlyList<CanonicalSkillPackage> packages)
    {
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        ArgumentNullException.ThrowIfNull(packages);

        Packages = Array.AsReadOnly(packages.ToArray());
    }

    /// <summary> Gets the descriptor that owns the package set. </summary>
    public SkillBundleDescriptor Descriptor { get; }

    /// <summary> Gets the complete canonical SKILL package set. </summary>
    public IReadOnlyList<CanonicalSkillPackage> Packages { get; }
}
