using MackySoft.AgentDistribution.Agents.Distribution;
using MackySoft.AgentDistribution.Agents.Installation.Targeting;

namespace MackySoft.AgentDistribution.Agents.Installation.Requests;

/// <summary> Represents one custom-agent uninstall request. </summary>
public sealed class AgentUninstallInput
{
    /// <summary> Initializes one immutable uninstall input. </summary>
    public AgentUninstallInput (AgentPackageCatalog catalog, AgentTargetRequest agentTargetRequest, bool dryRun = false, bool force = false)
    {
        Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        AgentTargetRequest = agentTargetRequest ?? throw new ArgumentNullException(nameof(agentTargetRequest));
        DryRun = dryRun;
        Force = force;
    }

    /// <summary> Gets the selected agents to remove. Resolved SKILL dependencies are never removed. </summary>
    public AgentPackageCatalog Catalog { get; }

    /// <summary> Gets the custom-agent artifact target. </summary>
    public AgentTargetRequest AgentTargetRequest { get; }

    /// <summary> Gets whether to plan without deletions. </summary>
    public bool DryRun { get; }

    /// <summary> Gets whether locally modified managed artifacts may be deleted. </summary>
    public bool Force { get; }
}
