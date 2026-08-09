namespace MackySoft.AgentSkills.Agents.Doctor;

/// <summary> Identifies the custom-agent contract area diagnosed by one result. </summary>
[VocabularyDefinition]
public enum AgentDoctorDiagnosticArea
{
    /// <summary> The canonical custom-agent package. </summary>
    [VocabularyText("package")]
    Package = 0,

    /// <summary> Host-specific generated artifacts. </summary>
    [VocabularyText("hostArtifact")]
    HostArtifact = 1,

    /// <summary> Installed artifact and ownership state. </summary>
    [VocabularyText("targetState")]
    TargetState = 2,
}
