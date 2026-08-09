namespace MackySoft.AgentDistribution.Hosts.Contracts;

/// <summary>Defines the execution hosts supported by this library version.</summary>
[VocabularyDefinition]
public enum HostKind
{
    /// <summary>Codex.</summary>
    [VocabularyText("codex")]
    Codex = 0,

    /// <summary>Claude Code.</summary>
    [VocabularyText("claude-code")]
    ClaudeCode = 1,

    /// <summary>GitHub Copilot.</summary>
    [VocabularyText("github-copilot")]
    GitHubCopilot = 2,
}
