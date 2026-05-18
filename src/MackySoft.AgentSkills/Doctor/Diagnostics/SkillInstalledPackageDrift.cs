using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Doctor.Diagnostics;

/// <summary> Represents the primary local drift category for one installed SKILL package. </summary>
/// <param name="Code"> The machine-readable drift code. </param>
/// <param name="Message"> The human-readable drift message. </param>
public sealed record SkillInstalledPackageDrift (
    SkillFailureCode Code,
    string Message);
