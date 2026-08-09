using MackySoft.AgentDistribution.Agents.Installation.Targeting;
using MackySoft.AgentDistribution.Hosts.Registration;
using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Hosts.GitHubCopilot;

/// <summary>Composes the GitHub Copilot host module.</summary>
internal static class GitHubCopilotHostRegistration
{
    /// <summary>Creates the complete GitHub Copilot registration.</summary>
    public static HostRegistration Create ()
    {
        return new HostRegistration(
            HostKind.GitHubCopilot,
            new GitHubCopilotSkillHostAdapter(),
            new GitHubCopilotAgentHostArtifactAdapter(),
            new AgentHostTargetPolicy(
                RootRelativePath.Parse(".github/agents"),
                RootRelativePath.Parse(".github/agent-distribution/agents"),
                new AgentUserTargetRootPolicy(
                    null,
                    null,
                    null,
                    RootRelativePath.Parse(".copilot/agents"),
                    RootRelativePath.Parse(".copilot/agent-distribution/agents")),
                RootRelativePath.Parse(".agent-distribution")));
    }
}
