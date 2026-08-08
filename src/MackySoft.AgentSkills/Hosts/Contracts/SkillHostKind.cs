namespace MackySoft.AgentSkills.Hosts.Contracts;

/// <summary> Defines the SKILL hosts supported by this library version. </summary>
[VocabularyDefinition]
public enum SkillHostKind
{
    /// <summary> Claude Code. </summary>
    [VocabularyText("claude")]
    Claude = 0,

    /// <summary> GitHub Copilot CLI. </summary>
    [VocabularyText("copilot")]
    Copilot = 1,

    /// <summary> OpenAI Codex. </summary>
    [VocabularyText("openai")]
    OpenAi = 2,
}
