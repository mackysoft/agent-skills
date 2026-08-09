namespace MackySoft.AgentSkills.OperationReports.Literals;

/// <summary> Defines custom-agent diagnostic areas exposed by operation reports. </summary>
[VocabularyDefinition]
public enum AgentDiagnosticArea
{
    [VocabularyText("package")]
    Package = 0,

    [VocabularyText("hostArtifact")]
    HostArtifact = 1,

    [VocabularyText("targetState")]
    TargetState = 2,
}
