using MackySoft.AgentDistribution.Agents.Sources;
using MackySoft.AgentDistribution.Shared;

namespace MackySoft.AgentDistribution.Hosts.Contracts;

/// <summary>Validates one host binding and generates that host's agent artifacts.</summary>
internal interface IAgentHostArtifactAdapter
{
    /// <summary>Validates host-owned binding JSON.</summary>
    SkillOperationResult<bool> ValidateBinding (string bindingJson);

    /// <summary>Generates host-owned files from a validated binding.</summary>
    AgentHostArtifactSet BuildArtifacts (AgentSourceMetadata metadata, string agentInstructions, string bindingJson);
}
