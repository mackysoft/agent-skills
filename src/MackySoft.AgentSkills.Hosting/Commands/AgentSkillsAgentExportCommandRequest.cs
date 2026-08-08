namespace MackySoft.AgentSkills.Hosting.Commands;

/// <summary> Represents raw input for a custom-agent export command. </summary>
public sealed class AgentSkillsAgentExportCommandRequest
{
    /// <summary> Initializes one export request. </summary>
    public AgentSkillsAgentExportCommandRequest (string? host = null, IReadOnlyList<string>? category = null, IReadOnlyList<string>? agent = null, string? output = null, string? format = null)
    {
        Host = host;
        Category = AgentSkillsCommandRequestOptionSnapshot.Create(category, nameof(category));
        Agent = AgentSkillsCommandRequestOptionSnapshot.Create(agent, nameof(agent));
        Output = output;
        Format = format;
    }

    /// <summary> Gets the raw host literal. </summary>
    public string? Host { get; }

    /// <summary> Gets raw selected custom-agent categories. </summary>
    public IReadOnlyList<string>? Category { get; }

    /// <summary> Gets raw selected exact custom-agent names. </summary>
    public IReadOnlyList<string>? Agent { get; }

    /// <summary> Gets the raw export output path. </summary>
    public string? Output { get; }

    /// <summary> Gets the raw export format literal. </summary>
    public string? Format { get; }
}
