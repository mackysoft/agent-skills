using MackySoft.AgentSkills.Shared.Text;

namespace MackySoft.AgentSkills.Doctor;

/// <summary> Defines SKILL doctor diagnostic severity. </summary>
public enum SkillDoctorSeverity
{
    /// <summary> Informational diagnostic. </summary>
    [ContractLiteral("info")]
    Info = 0,

    /// <summary> Error diagnostic. </summary>
    [ContractLiteral("error")]
    Error = 1,
}
