using MackySoft.AgentSkills.Hosts.Claude;
using MackySoft.AgentSkills.Hosts.Contracts;
using MackySoft.AgentSkills.Hosts.Copilot;
using MackySoft.AgentSkills.Hosts.OpenAi;
using MackySoft.AgentSkills.Hosts.Registration;

namespace MackySoft.AgentSkills.Hosts.Defaults;

/// <summary> Creates the default supported SKILL host adapters. </summary>
public static class DefaultSkillHostAdapters
{
    /// <summary> Creates the deterministic default supported host adapter set. </summary>
    /// <returns> The default host adapter set used by generation, validation, and runtime commands. </returns>
    public static SkillHostAdapterSet CreateSet ()
    {
        return new SkillHostAdapterSet(CreateAdapters());
    }

    private static IReadOnlyList<ISkillHostAdapter> CreateAdapters ()
    {
        return
        [
            new ClaudeSkillHostAdapter(),
            new CopilotSkillHostAdapter(),
            new OpenAiSkillHostAdapter(),
        ];
    }
}
