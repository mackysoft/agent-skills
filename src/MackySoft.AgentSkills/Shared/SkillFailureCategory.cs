namespace MackySoft.AgentSkills.Shared;

/// <summary> Defines product-neutral failure categories for SKILL operation failures. </summary>
public enum SkillFailureCategory
{
    /// <summary> The failure was caused by an invalid command or request input value. </summary>
    InvalidInput = 0,

    /// <summary> The failure was caused by a path that escapes an allowed boundary or cannot be treated safely. </summary>
    UnsafePath = 1,

    /// <summary> The requested host is not registered as a supported SKILL host. </summary>
    UnsupportedHost = 2,

    /// <summary> The requested install scope is not supported by the selected host. </summary>
    UnsupportedScope = 3,

    /// <summary> The user-scope target root cannot be resolved from the current environment. </summary>
    UserTargetUnavailable = 4,

    /// <summary> A canonical or installed SKILL manifest is missing, malformed, or inconsistent. </summary>
    ManifestInvalid = 5,

    /// <summary> A source SKILL definition is missing, malformed, or inconsistent. </summary>
    SourceInvalid = 6,

    /// <summary> An installed target is outdated, drifted, or contains local modifications. </summary>
    DriftOrLocalModification = 7,

    /// <summary> The target directory is not managed by an Agent Skills manifest. </summary>
    UnmanagedTarget = 8,

    /// <summary> A managed target identifies a different SKILL name than the requested package. </summary>
    NameCollision = 9,

    /// <summary> The target root or directory is managed for a different host. </summary>
    HostConflict = 10,

    /// <summary> The target directory or package content could not be read. </summary>
    ReadFailure = 11,

    /// <summary> The target directory, output path, or filesystem transaction could not be written. </summary>
    WriteOrFileSystemFailure = 12,

    /// <summary> The failure code is not recognized by this version of AgentSkills. </summary>
    UnexpectedInternalFailure = 13,

    /// <summary> An installed managed target belongs to the product catalog but is no longer bundled by it. </summary>
    RemovedFromCatalog = 14,
}
