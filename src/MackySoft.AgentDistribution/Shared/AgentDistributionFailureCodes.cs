namespace MackySoft.AgentDistribution.Shared;

/// <summary> Defines machine-readable failure codes for Agent Distribution operations. </summary>
public static class AgentDistributionFailureCodes
{
    /// <summary> Gets the code emitted when a product-independent command or request input value is invalid. </summary>
    public static readonly AgentDistributionFailureCode InputInvalid = new("AGENT_DISTRIBUTION_INPUT_INVALID");

    /// <summary> Gets the code emitted when the requested host is not supported by the global host adapter set. </summary>
    public static readonly AgentDistributionFailureCode HostUnsupported = new("AGENT_DISTRIBUTION_HOST_UNSUPPORTED");

    /// <summary> Gets the code emitted when the requested install scope is not supported by the selected host. </summary>
    public static readonly AgentDistributionFailureCode ScopeUnsupported = new("AGENT_DISTRIBUTION_SCOPE_UNSUPPORTED");

    /// <summary> Gets the code emitted when a source definition is missing or invalid. </summary>
    public static readonly AgentDistributionFailureCode SourceInvalid = new("AGENT_DISTRIBUTION_SOURCE_INVALID");

    /// <summary> Gets the code emitted when source and generated bundle versions cannot be reconciled safely. </summary>
    public static readonly AgentDistributionFailureCode BundleVersionConflict = new("AGENT_DISTRIBUTION_BUNDLE_VERSION_CONFLICT");

    /// <summary> Gets the code emitted by a check-only build when canonical generated output requires changes. </summary>
    public static readonly AgentDistributionFailureCode BundleUpdateRequired = new("AGENT_DISTRIBUTION_BUNDLE_UPDATE_REQUIRED");

    /// <summary> Gets the code emitted when a canonical manifest is missing or invalid. </summary>
    public static readonly AgentDistributionFailureCode ManifestInvalid = new("AGENT_DISTRIBUTION_MANIFEST_INVALID");

    /// <summary> Gets the code emitted when a requested path escapes the allowed target boundary. </summary>
    public static readonly AgentDistributionFailureCode PathUnsafe = new("AGENT_DISTRIBUTION_PATH_UNSAFE");

    /// <summary> Gets the code emitted when a user-scope host package root cannot be resolved from the current environment. </summary>
    public static readonly AgentDistributionFailureCode UserTargetUnavailable = new("AGENT_DISTRIBUTION_USER_TARGET_UNAVAILABLE");

    /// <summary> Gets the code emitted when the target directory is not managed by a canonical Agent Distribution manifest. </summary>
    public static readonly AgentDistributionFailureCode InstallTargetUnmanaged = new("AGENT_DISTRIBUTION_INSTALL_TARGET_UNMANAGED");

    /// <summary> Gets the code emitted when the target directory contains different package content. </summary>
    public static readonly AgentDistributionFailureCode InstallTargetDigestMismatch = new("AGENT_DISTRIBUTION_INSTALL_TARGET_DIGEST_MISMATCH");

    /// <summary> Gets the code emitted when installed manifest metadata drifted. </summary>
    public static readonly AgentDistributionFailureCode InstallTargetManifestDigestMismatch = new("AGENT_DISTRIBUTION_INSTALL_TARGET_MANIFEST_DIGEST_MISMATCH");

    /// <summary> Gets the code emitted when installed host-independent package content drifted. </summary>
    public static readonly AgentDistributionFailureCode InstallTargetContentDigestMismatch = new("AGENT_DISTRIBUTION_INSTALL_TARGET_CONTENT_DIGEST_MISMATCH");

    /// <summary> Gets the code emitted when installed package frontmatter drifted. </summary>
    public static readonly AgentDistributionFailureCode InstallTargetFrontmatterDigestMismatch = new("AGENT_DISTRIBUTION_INSTALL_TARGET_FRONTMATTER_DIGEST_MISMATCH");

    /// <summary> Gets the code emitted when an installed host artifact drifted. </summary>
    public static readonly AgentDistributionFailureCode InstallTargetHostArtifactDigestMismatch = new("AGENT_DISTRIBUTION_INSTALL_TARGET_HOST_ARTIFACT_DIGEST_MISMATCH");

    /// <summary> Gets the code emitted when the installed file set drifted. </summary>
    public static readonly AgentDistributionFailureCode InstallTargetFileSetMismatch = new("AGENT_DISTRIBUTION_INSTALL_TARGET_FILE_SET_MISMATCH");

    /// <summary> Gets the code emitted when the installed package is clean but older than the bundled package. </summary>
    public static readonly AgentDistributionFailureCode InstallTargetOutdated = new("AGENT_DISTRIBUTION_INSTALL_TARGET_OUTDATED");

    /// <summary> Gets the code emitted when the installed package is clean but newer than the bundled package. </summary>
    public static readonly AgentDistributionFailureCode InstallTargetVersionAhead = new("AGENT_DISTRIBUTION_INSTALL_TARGET_VERSION_AHEAD");

    /// <summary> Gets the code emitted when an installed managed package no longer exists in the current catalog. </summary>
    public static readonly AgentDistributionFailureCode InstallTargetRemovedFromCatalog = new("AGENT_DISTRIBUTION_INSTALL_TARGET_REMOVED_FROM_CATALOG");

    /// <summary> Gets the code emitted when a managed target identifies a different package name. </summary>
    public static readonly AgentDistributionFailureCode InstallTargetNameCollision = new("AGENT_DISTRIBUTION_INSTALL_TARGET_NAME_COLLISION");

    /// <summary> Gets the code emitted when the target root appears to contain materialized output for another host. </summary>
    public static readonly AgentDistributionFailureCode InstallTargetHostConflict = new("AGENT_DISTRIBUTION_INSTALL_TARGET_HOST_CONFLICT");

    /// <summary> Gets the code emitted when one safe bundle target root cannot be selected because compatible roots are split or a catalog directory is occupied by a flat package. </summary>
    public static readonly AgentDistributionFailureCode InstallTargetRootConflict = new("AGENT_DISTRIBUTION_INSTALL_TARGET_ROOT_CONFLICT");

    /// <summary> Gets the code emitted when an installed package contains local modifications. </summary>
    public static readonly AgentDistributionFailureCode InstallTargetLocalModification = new("AGENT_DISTRIBUTION_INSTALL_TARGET_LOCAL_MODIFICATION");

    /// <summary> Gets the code emitted when the target directory could not be read for planning. </summary>
    public static readonly AgentDistributionFailureCode InstallTargetReadFailed = new("AGENT_DISTRIBUTION_INSTALL_TARGET_READ_FAILED");

    /// <summary> Gets the code emitted when the target directory could not be written atomically. </summary>
    public static readonly AgentDistributionFailureCode InstallTargetWriteFailed = new("AGENT_DISTRIBUTION_INSTALL_TARGET_WRITE_FAILED");
}
