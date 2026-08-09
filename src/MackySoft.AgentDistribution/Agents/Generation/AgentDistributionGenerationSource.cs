using MackySoft.AgentDistribution.Agents.Sources;
using MackySoft.AgentDistribution.Bundles;
using MackySoft.AgentDistribution.Sources;

namespace MackySoft.AgentDistribution.Agents.Generation;

/// <summary> Holds the fully validated v2 mixed source snapshot used by generation. </summary>
internal sealed class AgentDistributionGenerationSource
{
    /// <summary> Initializes a source snapshot. </summary>
    public AgentDistributionGenerationSource (AgentDistributionBundleDefinition bundleDefinition, IReadOnlyList<SkillSourceDefinition> skills, IReadOnlyList<AgentSourceDefinition> agents)
    {
        BundleDefinition = bundleDefinition ?? throw new ArgumentNullException(nameof(bundleDefinition));
        ArgumentNullException.ThrowIfNull(skills);
        ArgumentNullException.ThrowIfNull(agents);
        if (skills.Count == 0 && agents.Count == 0)
        {
            throw new ArgumentException("A v2 bundle must contain at least one skill or agent.");
        }

        Skills = Array.AsReadOnly(skills.ToArray());
        Agents = Array.AsReadOnly(agents.ToArray());
    }

    /// <summary> Gets the bundle identity. </summary>
    public AgentDistributionBundleDefinition BundleDefinition { get; }

    /// <summary> Gets source skills. </summary>
    public IReadOnlyList<SkillSourceDefinition> Skills { get; }

    /// <summary> Gets source agents. </summary>
    public IReadOnlyList<AgentSourceDefinition> Agents { get; }
}
