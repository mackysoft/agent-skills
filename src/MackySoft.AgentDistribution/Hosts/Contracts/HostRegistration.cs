namespace MackySoft.AgentDistribution.Hosts.Contracts;

/// <summary> Associates one execution host with its adapter and target contracts. </summary>
internal sealed class HostRegistration
{
    /// <summary> Initializes one complete host registration. </summary>
    internal HostRegistration (
        HostKind host,
        ISkillHostAdapter skillAdapter,
        IAgentHostArtifactAdapter agentArtifactAdapter,
        AgentHostTargetPolicy agentTargetPolicy)
    {
        if (!Vocabulary.IsDefined(host))
        {
            throw new ArgumentOutOfRangeException(nameof(host), host, "Unsupported host value.");
        }

        Host = host;
        SkillAdapter = skillAdapter ?? throw new ArgumentNullException(nameof(skillAdapter));
        AgentArtifactAdapter = agentArtifactAdapter ?? throw new ArgumentNullException(nameof(agentArtifactAdapter));
        AgentTargetPolicy = agentTargetPolicy ?? throw new ArgumentNullException(nameof(agentTargetPolicy));
    }

    /// <summary> Gets the execution host. </summary>
    internal HostKind Host { get; }

    /// <summary> Gets the Skill target and materialization descriptor. </summary>
    internal SkillHostDescriptor Skill => SkillAdapter.Descriptor;

    /// <summary> Gets the Agent target policy. </summary>
    internal AgentHostTargetPolicy AgentTargetPolicy { get; }

    /// <summary> Gets the Skill artifact adapter. </summary>
    internal ISkillHostAdapter SkillAdapter { get; }

    /// <summary> Gets the Agent artifact adapter. </summary>
    internal IAgentHostArtifactAdapter AgentArtifactAdapter { get; }
}
