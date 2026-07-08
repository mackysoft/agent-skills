namespace MackySoft.AgentSkills.Hosting.Commands;

/// <summary> Represents raw list command input. </summary>
/// <param name="Tier"> The raw tier option values. Empty or <see langword="null" /> selects all configured tiers. </param>
/// <param name="Skill"> The raw exact SKILL name option values. Empty or <see langword="null" /> omits the name filter. </param>
public sealed record AgentSkillsListCommandRequest (
    string[]? Tier = null,
    string[]? Skill = null);
