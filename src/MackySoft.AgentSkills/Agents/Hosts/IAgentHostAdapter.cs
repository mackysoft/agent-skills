using MackySoft.AgentSkills.Agents.Sources;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Agents.Hosts;

/// <summary> Validates one host binding and generates that host's agent artifacts. </summary>
internal interface IAgentHostAdapter
{
    /// <summary> Gets the host-owned target and installation-state policy. </summary>
    AgentHostDescriptor Descriptor { get; }

    /// <summary> Gets the host that owns this binding contract. </summary>
    AgentHostKind HostId { get; }

    /// <summary> Validates host-owned binding JSON. </summary>
    SkillOperationResult<bool> ValidateBinding (string bindingJson);

    /// <summary> Generates host-owned files from a validated binding. </summary>
    AgentHostArtifactSet BuildArtifacts (AgentSourceMetadata metadata, string agentInstructions, string bindingJson);
}
