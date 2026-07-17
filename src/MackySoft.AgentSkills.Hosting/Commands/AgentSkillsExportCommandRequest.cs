namespace MackySoft.AgentSkills.Hosting.Commands;

/// <summary> Represents raw export command input. </summary>
public sealed class AgentSkillsExportCommandRequest
{
    /// <summary> Initializes raw export command input. </summary>
    /// <param name="host"> The required host literal. </param>
    /// <param name="category"> The raw category option values. Required when <paramref name="skill" /> is omitted. </param>
    /// <param name="skill"> The raw exact SKILL name option values. Required when <paramref name="category" /> is omitted. </param>
    /// <param name="output"> The required output directory or zip file path. </param>
    /// <param name="format"> The export format literal. Empty or <see langword="null" /> uses <c>directory</c>. </param>
    /// <exception cref="ArgumentException"> Thrown when an option collection contains a <see langword="null" /> item. </exception>
    public AgentSkillsExportCommandRequest (
        string? host = null,
        IReadOnlyList<string>? category = null,
        IReadOnlyList<string>? skill = null,
        string? output = null,
        string? format = null)
    {
        Host = host;
        Category = AgentSkillsCommandRequestOptionSnapshot.Create(category, nameof(category));
        Skill = AgentSkillsCommandRequestOptionSnapshot.Create(skill, nameof(skill));
        Output = output;
        Format = format;
    }

    /// <summary> Gets the required host literal. </summary>
    public string? Host { get; }

    /// <summary> Gets the raw category option value snapshot. </summary>
    public IReadOnlyList<string>? Category { get; }

    /// <summary> Gets the raw exact SKILL name option value snapshot. </summary>
    public IReadOnlyList<string>? Skill { get; }

    /// <summary> Gets the required output directory or zip file path. </summary>
    public string? Output { get; }

    /// <summary> Gets the export format literal. </summary>
    public string? Format { get; }
}
