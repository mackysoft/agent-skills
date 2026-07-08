using MackySoft.AgentSkills.Shared.Text;

namespace MackySoft.AgentSkills.Installation.Results;

/// <summary> Defines the analyzed target state kind exposed on operation action results. </summary>
public enum SkillActionTargetStateKind
{
    /// <summary> The target skill directory is absent. </summary>
    [ContractLiteral("missing")]
    Missing = 0,

    /// <summary> The target matches the current canonical package and requested host. </summary>
    [ContractLiteral("current")]
    Current = 1,

    /// <summary> The target is managed and clean, but does not match the current canonical package. </summary>
    [ContractLiteral("cleanOutdated")]
    CleanOutdated = 2,

    /// <summary> The target is managed but contains local modifications. </summary>
    [ContractLiteral("localModification")]
    LocalModified = 3,

    /// <summary> The target skill directory exists without an Agent Skills manifest. </summary>
    [ContractLiteral("unmanagedTarget")]
    Unmanaged = 4,

    /// <summary> The target manifest metadata or manifest digest drifted. </summary>
    [ContractLiteral("manifestDrift")]
    ManifestDrift = 5,

    /// <summary> The host-independent SKILL body or references drifted. </summary>
    [ContractLiteral("commonContentDrift")]
    CommonContentDrift = 6,

    /// <summary> The host-specific SKILL.md frontmatter drifted. </summary>
    [ContractLiteral("frontmatterDrift")]
    FrontmatterDrift = 7,

    /// <summary> The host-specific materialized artifact drifted. </summary>
    [ContractLiteral("hostArtifactDrift")]
    HostArtifactDrift = 8,

    /// <summary> The installed managed file set drifted. </summary>
    [ContractLiteral("fileSetDrift")]
    FileSetDrift = 9,

    /// <summary> The target directory is managed for a different SKILL name. </summary>
    [ContractLiteral("nameCollision")]
    NameCollision = 10,

    /// <summary> The target directory is materialized for a different host. </summary>
    [ContractLiteral("hostConflict")]
    HostConflict = 11,

    /// <summary> The target is managed and clean, but was generated from a newer SKILL bundle. </summary>
    [ContractLiteral("versionAhead")]
    VersionAhead = 12,

    /// <summary> The target is managed and clean, but no longer exists in the current catalog. </summary>
    [ContractLiteral("removedFromCatalog")]
    RemovedFromCatalog = 13,
}
