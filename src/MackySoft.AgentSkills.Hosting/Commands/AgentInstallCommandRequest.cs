namespace MackySoft.AgentSkills.Hosting.Commands;

/// <summary> Represents raw input for a custom-agent install command. </summary>
public sealed class AgentInstallCommandRequest
{
    /// <summary> Initializes one install request. </summary>
    public AgentInstallCommandRequest (string? host = null, IReadOnlyList<string>? category = null, IReadOnlyList<string>? agent = null, string? scope = null, string? repositoryRoot = null, string? agentTargetDir = null, string? skillTargetDir = null, bool dryRun = false, bool force = false, bool printDiff = false)
    {
        Host = host;
        Category = CommandOptionValues.Snapshot(category, nameof(category));
        Agent = CommandOptionValues.Snapshot(agent, nameof(agent));
        Scope = scope;
        RepositoryRoot = repositoryRoot;
        AgentTargetDir = agentTargetDir;
        SkillTargetDir = skillTargetDir;
        DryRun = dryRun;
        Force = force;
        PrintDiff = printDiff;
    }

    /// <summary> Gets the raw host literal. </summary>
    public string? Host { get; }

    /// <summary> Gets raw selected custom-agent categories. </summary>
    public IReadOnlyList<string>? Category { get; }

    /// <summary> Gets raw selected exact custom-agent names. </summary>
    public IReadOnlyList<string>? Agent { get; }

    /// <summary> Gets the raw install scope literal. </summary>
    public string? Scope { get; }

    /// <summary> Gets the optional project repository root. </summary>
    public string? RepositoryRoot { get; }

    /// <summary> Gets the optional exact custom-agent artifact target root. </summary>
    public string? AgentTargetDir { get; }

    /// <summary> Gets the optional exact resolved-SKILL target root. </summary>
    public string? SkillTargetDir { get; }

    /// <summary> Gets whether to plan without writing. </summary>
    public bool DryRun { get; }

    /// <summary> Gets whether eligible managed content may be replaced. </summary>
    public bool Force { get; }

    /// <summary> Gets whether the SKILL operation includes file diffs. </summary>
    public bool PrintDiff { get; }
}
