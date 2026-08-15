using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Hosts.ClaudeCode;

/// <summary> Composes the Claude Code host contracts. </summary>
internal static class ClaudeCodeHostFactory
{
    /// <summary> Creates the complete Claude Code host registration. </summary>
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
