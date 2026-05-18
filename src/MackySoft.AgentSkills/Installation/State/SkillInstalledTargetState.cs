namespace MackySoft.AgentSkills.Installation.State;

/// <summary> Represents the analyzed state of one installed SKILL target. </summary>
/// <param name="Kind"> The target state kind. </param>
public sealed record SkillInstalledTargetState (SkillInstalledTargetStateKind Kind);
