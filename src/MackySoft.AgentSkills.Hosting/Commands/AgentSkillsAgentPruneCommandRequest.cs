namespace MackySoft.AgentSkills.Hosting.Commands;

/// <summary> Represents raw input for a custom-agent prune command. </summary>
public sealed class AgentSkillsAgentPruneCommandRequest
{
    /// <summary> Initializes one prune request. </summary>
    public AgentSkillsAgentPruneCommandRequest (string? host = null, IReadOnlyList<string>? category = null, IReadOnlyList<string>? agent = null, string? scope = null, string? repositoryRoot = null, string? agentTargetDir = null, bool dryRun = false, bool force = false)
    {
        Host = host;
        Category = AgentSkillsCommandRequestOptionSnapshot.Create(category, nameof(category));
        Agent = AgentSkillsCommandRequestOptionSnapshot.Create(agent, nameof(agent));
        Scope = scope;
        RepositoryRoot = repositoryRoot;
        AgentTargetDir = agentTargetDir;
        DryRun = dryRun;
        Force = force;
    }

    /// <summary> Gets the raw host literal. </summary>
    public string? Host { get; }

    /// <summary> Gets raw custom-agent category filters. </summary>
    public IReadOnlyList<string>? Category { get; }

    /// <summary> Gets raw exact custom-agent name filters, including names removed from the current catalog. </summary>
    public IReadOnlyList<string>? Agent { get; }

    /// <summary> Gets the raw install scope literal. </summary>
    public string? Scope { get; }

    /// <summary> Gets the optional project repository root. </summary>
    public string? RepositoryRoot { get; }

    /// <summary> Gets the optional exact custom-agent artifact target root. </summary>
    public string? AgentTargetDir { get; }

    /// <summary> Gets whether to plan without deleting. </summary>
    public bool DryRun { get; }

    /// <summary> Gets whether locally modified managed content may be deleted. </summary>
    public bool Force { get; }
}
