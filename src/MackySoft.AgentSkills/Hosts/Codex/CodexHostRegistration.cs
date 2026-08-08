using MackySoft.AgentSkills.Agents.Installation.Targeting;
using MackySoft.AgentSkills.Hosts.Registration;
using MackySoft.FileSystem;

namespace MackySoft.AgentSkills.Hosts.Codex;

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
                RootRelativePath.Parse(".codex/agent-skills/agents"),
                new AgentUserTargetRootPolicy(
                    "CODEX_HOME",
                    RootRelativePath.Parse("agents"),
                    RootRelativePath.Parse("agent-skills/agents"),
                    RootRelativePath.Parse(".codex/agents"),
                    RootRelativePath.Parse(".codex/agent-skills/agents")),
                RootRelativePath.Parse(".agent-skills")));
    }
}
