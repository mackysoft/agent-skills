namespace MackySoft.AgentDistribution.Hosting.Commands;

/// <summary> Represents raw input for a custom-agent doctor command. </summary>
public sealed class AgentDoctorCommandRequest
{
    /// <summary> Initializes one doctor request. </summary>
    public AgentDoctorCommandRequest (string? host = null, IReadOnlyList<string>? category = null, IReadOnlyList<string>? agent = null, string? scope = null, string? repositoryRoot = null, string? agentTargetDir = null, string? skillTargetDir = null)
    {
        Host = host;
        Category = CommandOptionValues.Snapshot(category, nameof(category));
        Agent = CommandOptionValues.Snapshot(agent, nameof(agent));
        Scope = scope;
        RepositoryRoot = repositoryRoot;
        AgentTargetDir = agentTargetDir;
        SkillTargetDir = skillTargetDir;
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
}
