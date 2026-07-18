namespace MackySoft.AgentSkills.Hosting.Commands;

/// <summary> Represents raw doctor command input. </summary>
public sealed class AgentSkillsDoctorCommandRequest
{
    /// <summary> Initializes raw doctor command input. </summary>
    /// <param name="host"> The required host literal. </param>
    /// <param name="category"> The raw category option values. Required when <paramref name="skill" /> is omitted. </param>
    /// <param name="skill"> The raw exact SKILL name option values. Required when <paramref name="category" /> is omitted. </param>
    /// <param name="scope"> The required scope literal. </param>
    /// <param name="repositoryRoot"> The optional repository root for project scope. </param>
    /// <param name="targetDir"> The optional exact bundle target root override. </param>
    /// <exception cref="ArgumentException"> Thrown when an option collection contains a <see langword="null" /> item. </exception>
    public AgentSkillsDoctorCommandRequest (
        string? host = null,
        IReadOnlyList<string>? category = null,
        IReadOnlyList<string>? skill = null,
        string? scope = null,
        string? repositoryRoot = null,
        string? targetDir = null)
    {
        Host = host;
        Category = AgentSkillsCommandRequestOptionSnapshot.Create(category, nameof(category));
        Skill = AgentSkillsCommandRequestOptionSnapshot.Create(skill, nameof(skill));
        Scope = scope;
        RepositoryRoot = repositoryRoot;
        TargetDir = targetDir;
    }

    /// <summary> Gets the required host literal. </summary>
    public string? Host { get; }

    /// <summary> Gets the raw category option value snapshot. </summary>
    public IReadOnlyList<string>? Category { get; }

    /// <summary> Gets the raw exact SKILL name option value snapshot. </summary>
    public IReadOnlyList<string>? Skill { get; }

    /// <summary> Gets the required scope literal. </summary>
    public string? Scope { get; }

    /// <summary> Gets the optional repository root for project scope. </summary>
    public string? RepositoryRoot { get; }

    /// <summary> Gets the optional exact bundle target root override. </summary>
    public string? TargetDir { get; }
}
