using MackySoft.AgentDistribution.Agents.Installation.Targeting;
using MackySoft.AgentDistribution.Distribution;
using MackySoft.AgentDistribution.Installation.Targeting;

namespace MackySoft.AgentDistribution.Agents.Doctor;

/// <summary> Represents separate custom-agent and SKILL targets for one doctor operation. </summary>
public sealed class AgentDoctorInput
{
    /// <summary> Initializes one immutable doctor input. </summary>
    public AgentDoctorInput (AgentPackageCatalog catalog, AgentTargetRequest agentTargetRequest, SkillInstallRequest skillTargetRequest)
    {
        Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        AgentTargetRequest = agentTargetRequest ?? throw new ArgumentNullException(nameof(agentTargetRequest));
        SkillTargetRequest = skillTargetRequest ?? throw new ArgumentNullException(nameof(skillTargetRequest));
        if (AgentTargetRequest.HostId != SkillTargetRequest.Host)
        {
            throw new ArgumentException("Custom-agent and resolved SKILL targets must use the same host.", nameof(skillTargetRequest));
        }
    }

    /// <summary> Gets selected agents and their resolved SKILL dependencies. </summary>
    public AgentPackageCatalog Catalog { get; }

    /// <summary> Gets the custom-agent target request. </summary>
    public AgentTargetRequest AgentTargetRequest { get; }

    /// <summary> Gets the independent SKILL target request. </summary>
    public SkillInstallRequest SkillTargetRequest { get; }
}
