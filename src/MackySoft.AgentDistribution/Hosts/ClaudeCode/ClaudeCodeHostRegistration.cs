using MackySoft.AgentDistribution.Agents.Installation.Targeting;
using MackySoft.AgentDistribution.Hosts.Registration;
using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Hosts.ClaudeCode;

/// <summary>Composes the Claude Code host module.</summary>
internal static class ClaudeCodeHostRegistration
{
    /// <summary>Creates the complete Claude Code registration.</summary>
    public static HostRegistration Create ()
    {
        return new HostRegistration(
            HostKind.ClaudeCode,
            new ClaudeCodeSkillHostAdapter(),
            new ClaudeCodeAgentHostArtifactAdapter(),
            new AgentHostTargetPolicy(
                RootRelativePath.Parse(".claude/agents"),
                RootRelativePath.Parse(".claude/agent-distribution/agents"),
                new AgentUserTargetRootPolicy(
                    null,
                    null,
                    null,
                    RootRelativePath.Parse(".claude/agents"),
                    RootRelativePath.Parse(".claude/agent-distribution/agents")),
                RootRelativePath.Parse(".agent-distribution")));
    }
}
