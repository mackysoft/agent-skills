namespace MackySoft.AgentSkills.Hosting.Commands;

/// <summary> Represents raw update command input. </summary>
/// <param name="Host"> The required host literal. </param>
/// <param name="Tier"> The raw tier option values. Required when <paramref name="Skill" /> is omitted. </param>
/// <param name="Skill"> The raw exact SKILL name option values. Required when <paramref name="Tier" /> is omitted. </param>
/// <param name="Scope"> The required scope literal. </param>
/// <param name="RepositoryRoot"> The optional repository root for project scope. </param>
/// <param name="TargetDir"> The optional host target root override. </param>
/// <param name="DryRun"> Whether to return the update plan without writing files. </param>
/// <param name="Force"> Whether force semantics are enabled. </param>
/// <param name="PrintDiff"> Whether file diffs are included in operation reports. </param>
public sealed record AgentSkillsUpdateCommandRequest (
    string? Host = null,
    string[]? Tier = null,
    string[]? Skill = null,
    string? Scope = null,
    string? RepositoryRoot = null,
    string? TargetDir = null,
    bool DryRun = false,
    bool Force = false,
    bool PrintDiff = false);
