namespace MackySoft.AgentSkills.Hosting.Commands;

/// <summary> Represents raw prune command input. </summary>
/// <param name="Host"> The required host literal. </param>
/// <param name="Tier"> The raw tier option values. Required when <paramref name="Skill" /> is omitted. </param>
/// <param name="Skill"> The raw exact SKILL name option values. Required when <paramref name="Tier" /> is omitted. </param>
/// <param name="Scope"> The required scope literal. </param>
/// <param name="RepositoryRoot"> The optional repository root for project scope. </param>
/// <param name="TargetDir"> The optional host target root override. </param>
/// <param name="DryRun"> Whether to return the prune plan without deleting files. </param>
/// <param name="Force"> Whether force semantics are enabled. </param>
public sealed record AgentSkillsPruneCommandRequest (
    string? Host = null,
    string[]? Tier = null,
    string[]? Skill = null,
    string? Scope = null,
    string? RepositoryRoot = null,
    string? TargetDir = null,
    bool DryRun = false,
    bool Force = false);
