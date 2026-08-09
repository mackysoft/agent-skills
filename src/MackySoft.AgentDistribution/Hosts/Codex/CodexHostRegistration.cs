using MackySoft.AgentDistribution.Agents.Installation.Targeting;
using MackySoft.AgentDistribution.Hosts.Registration;
using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Hosts.Codex;

/// <summary>Composes the Codex host module.</summary>
internal static class CodexHostRegistration
{
    /// <summary>Creates the complete Codex registration.</summary>
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
