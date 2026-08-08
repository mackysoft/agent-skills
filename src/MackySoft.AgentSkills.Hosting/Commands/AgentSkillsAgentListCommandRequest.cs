namespace MackySoft.AgentSkills.Hosting.Commands;

/// <summary> Represents raw input for a custom-agent list command. </summary>
public sealed class AgentSkillsAgentListCommandRequest
{
    /// <summary> Initializes one list request. </summary>
    public AgentSkillsAgentListCommandRequest (IReadOnlyList<string>? category = null, IReadOnlyList<string>? agent = null)
    {
        Category = AgentSkillsCommandRequestOptionSnapshot.Create(category, nameof(category));
        Agent = AgentSkillsCommandRequestOptionSnapshot.Create(agent, nameof(agent));
    }

    /// <summary> Gets raw selected custom-agent categories. </summary>
    public IReadOnlyList<string>? Category { get; }

    /// <summary> Gets raw selected exact custom-agent names. </summary>
    public IReadOnlyList<string>? Agent { get; }
}
