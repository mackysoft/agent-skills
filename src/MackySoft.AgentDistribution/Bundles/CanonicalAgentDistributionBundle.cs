using MackySoft.AgentDistribution.Agents.Packaging;
using MackySoft.AgentDistribution.Packaging.Canonical;

namespace MackySoft.AgentDistribution.Bundles;

/// <summary> Represents a validated v2 generated bundle with separate skill and agent namespaces. </summary>
public sealed class CanonicalAgentDistributionBundle
{
    /// <summary> Initializes a mixed canonical bundle. </summary>
    internal CanonicalAgentDistributionBundle (AgentDistributionBundleDescriptor descriptor, IReadOnlyList<CanonicalSkillPackage> skills, IReadOnlyList<CanonicalAgentPackage> agents)
    {
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        ArgumentNullException.ThrowIfNull(skills);
        ArgumentNullException.ThrowIfNull(agents);
        if (skills.Count == 0 && agents.Count == 0)
        {
            throw new ArgumentException("A mixed bundle must contain at least one package.");
        }

        if (skills.Any(static package => package is null) || agents.Any(static package => package is null) || skills.GroupBy(static package => package.Manifest.SkillName).Any(static group => group.Count() != 1) || agents.GroupBy(static package => package.Manifest.AgentName).Any(static group => group.Count() != 1))
        {
            throw new ArgumentException("Mixed bundle package identities must be unique.");
        }

        Skills = Array.AsReadOnly(skills.OrderBy(static package => package.Manifest.SkillName.Value, StringComparer.Ordinal).ToArray());
        Agents = Array.AsReadOnly(agents.OrderBy(static package => package.Manifest.AgentName.Value, StringComparer.Ordinal).ToArray());
    }

    /// <summary> Gets root descriptor. </summary>
    public AgentDistributionBundleDescriptor Descriptor { get; }
    /// <summary> Gets skill packages. </summary>
    public IReadOnlyList<CanonicalSkillPackage> Skills { get; }
    /// <summary> Gets agent packages. </summary>
    public IReadOnlyList<CanonicalAgentPackage> Agents { get; }
}
