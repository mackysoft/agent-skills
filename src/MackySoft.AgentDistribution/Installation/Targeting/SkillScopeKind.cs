namespace MackySoft.AgentDistribution.Installation.Targeting;

/// <summary> Defines supported SKILL install scopes. </summary>
[VocabularyDefinition]
public enum SkillScopeKind
{
    /// <summary> Project-local installation under a repository root. </summary>
    [VocabularyText("project")]
    Project = 0,

    /// <summary> User-local installation under the target host's personal SKILL root. </summary>
    [VocabularyText("user")]
    User = 1,
}
