using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Hosts.Codex;

/// <summary> Composes the Codex host contracts. </summary>
internal static class CodexHostFactory
{
    /// <summary> Creates the complete Codex host registration. </summary>
    public static HostRegistration Create ()
    {
        return new HostRegistration(
            HostKind.Codex,
            new CodexSkillHostAdapter(),
            new CodexAgentHostArtifactAdapter(),
            new AgentHostTargetPolicy(
                RootRelativePath.Parse(".codex/agents"),
                RootRelativePath.Parse(".codex/agent-distribution/agents"),
                new AgentUserTargetRootPolicy(
                    "CODEX_HOME",
                    RootRelativePath.Parse("agents"),
                    RootRelativePath.Parse("agent-distribution/agents"),
                    RootRelativePath.Parse(".codex/agents"),
                    RootRelativePath.Parse(".codex/agent-distribution/agents")),
                RootRelativePath.Parse(".agent-distribution")));
    }
}
