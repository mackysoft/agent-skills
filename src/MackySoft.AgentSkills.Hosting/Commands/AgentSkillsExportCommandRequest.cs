namespace MackySoft.AgentSkills.Hosting.Commands;

/// <summary> Represents raw export command input. </summary>
/// <param name="Host"> The required host literal. </param>
/// <param name="Tier"> The raw tier option values. Required when <paramref name="Skill" /> is omitted. </param>
/// <param name="Skill"> The raw exact SKILL name option values. Required when <paramref name="Tier" /> is omitted. </param>
/// <param name="Output"> The required output directory or zip file path. </param>
/// <param name="Format"> The export format literal. Empty or <see langword="null" /> uses <c>directory</c>. </param>
public sealed record AgentSkillsExportCommandRequest (
    string? Host = null,
    string[]? Tier = null,
    string[]? Skill = null,
    string? Output = null,
    string? Format = null);
