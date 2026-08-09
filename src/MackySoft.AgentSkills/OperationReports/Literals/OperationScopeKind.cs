namespace MackySoft.AgentSkills.OperationReports.Literals;

/// <summary> Defines the scope represented by a package operation report. </summary>
[VocabularyDefinition]
public enum OperationScopeKind
{
    /// <summary> The operation targets a repository-owned location. </summary>
    [VocabularyText("project")]
    Project = 0,

    /// <summary> The operation targets a current-user location. </summary>
    [VocabularyText("user")]
    User = 1,
}
