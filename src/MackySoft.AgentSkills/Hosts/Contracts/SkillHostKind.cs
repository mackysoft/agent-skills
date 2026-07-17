using MackySoft.AgentSkills.Shared.Text;

namespace MackySoft.AgentSkills.Hosts.Contracts;

/// <summary> Defines the SKILL hosts supported by this library version. </summary>
public enum SkillHostKind
{
    /// <summary> Claude Code. </summary>
    [ContractLiteral("claude")]
    Claude = 0,

    /// <summary> GitHub Copilot CLI. </summary>
    [ContractLiteral("copilot")]
    Copilot = 1,

    /// <summary> OpenAI Codex. </summary>
    [ContractLiteral("openai")]
    OpenAi = 2,
}
