using MackySoft.AgentDistribution.Agents.Installation.Targeting;
using MackySoft.AgentDistribution.Distribution;
using MackySoft.AgentDistribution.Installation.Targeting;

namespace MackySoft.AgentDistribution.Agents.Installation.Requests;

/// <summary> Represents one custom-agent update request and its separate SKILL target. </summary>
public sealed class AgentUpdateInput
{
    /// <summary> Initializes one immutable update input. </summary>
    public AgentUpdateInput (AgentPackageCatalog catalog, AgentTargetRequest agentTargetRequest, SkillInstallRequest skillTargetRequest, bool dryRun = false, bool force = false, bool printDiff = false)
    {
        Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        AgentTargetRequest = agentTargetRequest ?? throw new ArgumentNullException(nameof(agentTargetRequest));
        SkillTargetRequest = skillTargetRequest ?? throw new ArgumentNullException(nameof(skillTargetRequest));
        if (AgentTargetRequest.HostId != SkillTargetRequest.Host)
        {
            throw new ArgumentException("Custom-agent and resolved SKILL targets must use the same host.", nameof(skillTargetRequest));
        }
        DryRun = dryRun;
        Force = force;
        PrintDiff = printDiff;
    }

    /// <summary> Gets selected agents and their resolved SKILL dependencies. </summary>
    public AgentPackageCatalog Catalog { get; }

    /// <summary> Gets the custom-agent artifact target. </summary>
    public AgentTargetRequest AgentTargetRequest { get; }

    /// <summary> Gets the independent SKILL installation target. </summary>
    public SkillInstallRequest SkillTargetRequest { get; }

    /// <summary> Gets whether to plan without writes. </summary>
    public bool DryRun { get; }

    /// <summary> Gets whether eligible managed targets may be replaced. </summary>
    public bool Force { get; }

    /// <summary> Gets whether the delegated SKILL plan includes diffs. </summary>
    public bool PrintDiff { get; }
}
