namespace MackySoft.AgentSkills.Hosting.Commands;

/// <summary> Represents output formatting options for the default Agent Skills command emitter. </summary>
/// <param name="Pretty"> Whether JSON output should be indented. </param>
public sealed record AgentSkillsCommandOutputOptions (bool Pretty = false);
