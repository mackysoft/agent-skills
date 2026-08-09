namespace MackySoft.AgentSkills.Shared;

/// <summary> Defines the analyzed state kind for one installed SKILL target. </summary>
[VocabularyDefinition]
public enum SkillTargetStateKind
{
    /// <summary> The target skill directory is absent. </summary>
    [VocabularyText("missing")]
    Missing = 0,

    /// <summary> The target matches the current canonical package and requested host. </summary>
    [VocabularyText("current")]
    Current = 1,

    /// <summary> The target is managed and clean, but does not match the current canonical package. </summary>
    [VocabularyText("cleanOutdated")]
    CleanOutdated = 2,

    /// <summary> The target is managed but contains local modifications. </summary>
    [VocabularyText("localModification")]
    LocalModified = 3,

    /// <summary> The target skill directory exists without an Agent Skills manifest. </summary>
    [VocabularyText("unmanagedTarget")]
    Unmanaged = 4,

    /// <summary> The target manifest metadata or manifest digest drifted. </summary>
    [VocabularyText("manifestDrift")]
    ManifestDrift = 5,

    /// <summary> The host-independent SKILL body or references drifted. </summary>
    [VocabularyText("commonContentDrift")]
    CommonContentDrift = 6,

    /// <summary> The host-specific SKILL.md frontmatter drifted. </summary>
    [VocabularyText("frontmatterDrift")]
    FrontmatterDrift = 7,

    /// <summary> The host-specific materialized artifact drifted. </summary>
    [VocabularyText("hostArtifactDrift")]
    HostArtifactDrift = 8,

    /// <summary> The installed managed file set drifted. </summary>
    [VocabularyText("fileSetDrift")]
    FileSetDrift = 9,

    /// <summary> The target directory is managed for a different SKILL name. </summary>
    [VocabularyText("nameCollision")]
    NameCollision = 10,

    /// <summary> The target directory is materialized for a different host. </summary>
    [VocabularyText("hostConflict")]
    HostConflict = 11,

    /// <summary> The target is managed and clean, but was generated from a newer SKILL bundle. </summary>
    [VocabularyText("versionAhead")]
    VersionAhead = 12,

    /// <summary> The target is managed and clean, but no longer exists in the current catalog. </summary>
    [VocabularyText("removedFromCatalog")]
    RemovedFromCatalog = 13,
}
