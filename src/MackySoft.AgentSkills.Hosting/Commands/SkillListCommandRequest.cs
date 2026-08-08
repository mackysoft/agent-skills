namespace MackySoft.AgentSkills.Hosting.Commands;

/// <summary> Represents raw list command input. </summary>
public sealed class SkillListCommandRequest
{
    /// <summary> Initializes raw list command input. </summary>
    /// <param name="category"> The raw category option values. Empty or <see langword="null" /> selects all categories present in the generated bundle. </param>
    /// <param name="skill"> The raw exact SKILL name option values. Empty or <see langword="null" /> omits the name filter. </param>
    /// <exception cref="ArgumentException"> Thrown when an option collection contains a <see langword="null" /> item. </exception>
    public SkillListCommandRequest (
        IReadOnlyList<string>? category = null,
        IReadOnlyList<string>? skill = null)
    {
        Category = CommandOptionValues.Snapshot(category, nameof(category));
        Skill = CommandOptionValues.Snapshot(skill, nameof(skill));
    }

    /// <summary> Gets the raw category option value snapshot. </summary>
    public IReadOnlyList<string>? Category { get; }

    /// <summary> Gets the raw exact SKILL name option value snapshot. </summary>
    public IReadOnlyList<string>? Skill { get; }
}
