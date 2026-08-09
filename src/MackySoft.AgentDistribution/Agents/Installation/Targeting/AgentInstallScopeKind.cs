namespace MackySoft.AgentDistribution.Agents.Installation.Targeting;

/// <summary> Defines the supported custom-agent installation scopes. </summary>
[VocabularyDefinition]
public enum AgentInstallScopeKind
{
    /// <summary> Installs under a repository root. </summary>
    [VocabularyText("project")]
    Project = 0,

    /// <summary> Installs under the current user's host root. </summary>
    [VocabularyText("user")]
    User = 1,
}
