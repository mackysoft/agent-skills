namespace MackySoft.AgentDistribution.Doctor;

/// <summary> Defines SKILL doctor diagnostic severity. </summary>
[VocabularyDefinition]
public enum SkillDoctorSeverity
{
    /// <summary> Informational diagnostic. </summary>
    [VocabularyText("info")]
    Info = 0,

    /// <summary> Error diagnostic. </summary>
    [VocabularyText("error")]
    Error = 1,
}
