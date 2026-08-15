using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Hosts.GitHubCopilot;

/// <summary> Composes the GitHub Copilot host contracts. </summary>
internal static class GitHubCopilotHostFactory
{
    /// <summary> Creates the complete GitHub Copilot host registration. </summary>
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
