namespace MackySoft.AgentSkills.Hosts.Contracts;

/// <summary> Defines the custom-agent hosts supported by this library version. </summary>
[VocabularyDefinition]
public enum AgentHostKind
{
    /// <summary> OpenAI Codex custom agents. </summary>
    [VocabularyText("openai")]
    OpenAi = 0,
}
