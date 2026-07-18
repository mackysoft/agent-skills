namespace MackySoft.AgentSkills.Hosting.Commands;

/// <summary> Represents raw uninstall command input. </summary>
public sealed class AgentSkillsUninstallCommandRequest
{
    /// <summary> Initializes raw uninstall command input. </summary>
    /// <param name="host"> The required host literal. </param>
    /// <param name="category"> The raw category option values. Required when <paramref name="skill" /> is omitted. </param>
    /// <param name="skill"> The raw exact SKILL name option values. Required when <paramref name="category" /> is omitted. </param>
    /// <param name="scope"> The required scope literal. </param>
    /// <param name="repositoryRoot"> The optional repository root for project scope. </param>
    /// <param name="targetDir"> The optional exact bundle target root override. </param>
    /// <param name="dryRun"> Whether to return the uninstall plan without deleting files. </param>
    /// <param name="force"> Whether force semantics are enabled. </param>
    /// <exception cref="ArgumentException"> Thrown when an option collection contains a <see langword="null" /> item. </exception>
    public AgentSkillsUninstallCommandRequest (
        string? host = null,
        IReadOnlyList<string>? category = null,
        IReadOnlyList<string>? skill = null,
        string? scope = null,
        string? repositoryRoot = null,
        string? targetDir = null,
        bool dryRun = false,
        bool force = false)
    {
        Host = host;
        Category = AgentSkillsCommandRequestOptionSnapshot.Create(category, nameof(category));
        Skill = AgentSkillsCommandRequestOptionSnapshot.Create(skill, nameof(skill));
        Scope = scope;
        RepositoryRoot = repositoryRoot;
        TargetDir = targetDir;
        DryRun = dryRun;
        Force = force;
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

    /// <summary> Gets whether to return the uninstall plan without deleting files. </summary>
    public bool DryRun { get; }

    /// <summary> Gets whether force semantics are enabled. </summary>
    public bool Force { get; }
}
