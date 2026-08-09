namespace MackySoft.AgentSkills.Hosts.Contracts;

/// <summary> Defines how one host organizes bundle targets under its default SKILL root. </summary>
[VocabularyDefinition]
public enum SkillBundleTargetRootLayout
{
    /// <summary> Every skill is installed directly under the host SKILL root. </summary>
    [VocabularyText("flat")]
    Flat = 0,

    /// <summary> Every bundle owns a catalog-ID directory under the host SKILL root. </summary>
    [VocabularyText("catalog-directory")]
    CatalogDirectory = 1,
}
