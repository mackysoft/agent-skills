namespace MackySoft.AgentDistribution.Shared;

/// <summary> Classifies Agent Distribution failure codes into product-neutral categories. </summary>
public static class AgentDistributionFailureClassifier
{
    /// <summary> Classifies one Agent Distribution failure. </summary>
    /// <param name="failure"> The failure to classify. Must not be <see langword="null" />. </param>
    /// <returns> The product-neutral category for <paramref name="failure" />. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="failure" /> is <see langword="null" />. </exception>
    public static AgentDistributionFailureCategory Classify (AgentDistributionFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return Classify(failure.Code);
    }

    /// <summary> Classifies one Agent Distribution failure code. </summary>
    /// <param name="code"> The failure code to classify. Unknown values are allowed. </param>
    /// <returns> The product-neutral category for <paramref name="code" />, or <see cref="AgentDistributionFailureCategory.UnexpectedInternalFailure" /> for unknown codes. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="code" /> is <see langword="null" />. </exception>
    public static AgentDistributionFailureCategory Classify (AgentDistributionFailureCode code)
    {
        ArgumentNullException.ThrowIfNull(code);
        if (code == AgentDistributionFailureCodes.InputInvalid)
        {
            return AgentDistributionFailureCategory.InvalidInput;
        }

        if (code == AgentDistributionFailureCodes.PathUnsafe)
        {
            return AgentDistributionFailureCategory.UnsafePath;
        }

        if (code == AgentDistributionFailureCodes.HostUnsupported)
        {
            return AgentDistributionFailureCategory.UnsupportedHost;
        }

        if (code == AgentDistributionFailureCodes.ScopeUnsupported)
        {
            return AgentDistributionFailureCategory.UnsupportedScope;
        }

        if (code == AgentDistributionFailureCodes.UserTargetUnavailable)
        {
            return AgentDistributionFailureCategory.UserTargetUnavailable;
        }

        if (code == AgentDistributionFailureCodes.ManifestInvalid)
        {
            return AgentDistributionFailureCategory.ManifestInvalid;
        }

        if (code == AgentDistributionFailureCodes.SourceInvalid
            || code == AgentDistributionFailureCodes.BundleVersionConflict)
        {
            return AgentDistributionFailureCategory.SourceInvalid;
        }

        if (code == AgentDistributionFailureCodes.BundleUpdateRequired)
        {
            return AgentDistributionFailureCategory.DriftOrLocalModification;
        }

        if (IsDriftOrLocalModification(code))
        {
            return AgentDistributionFailureCategory.DriftOrLocalModification;
        }

        if (code == AgentDistributionFailureCodes.InstallTargetRemovedFromCatalog)
        {
            return AgentDistributionFailureCategory.RemovedFromCatalog;
        }

        if (code == AgentDistributionFailureCodes.InstallTargetUnmanaged)
        {
            return AgentDistributionFailureCategory.UnmanagedTarget;
        }

        if (code == AgentDistributionFailureCodes.InstallTargetNameCollision)
        {
            return AgentDistributionFailureCategory.NameCollision;
        }

        if (code == AgentDistributionFailureCodes.InstallTargetHostConflict)
        {
            return AgentDistributionFailureCategory.HostConflict;
        }

        if (code == AgentDistributionFailureCodes.InstallTargetRootConflict)
        {
            return AgentDistributionFailureCategory.TargetRootConflict;
        }

        if (code == AgentDistributionFailureCodes.InstallTargetReadFailed)
        {
            return AgentDistributionFailureCategory.ReadFailure;
        }

        if (code == AgentDistributionFailureCodes.InstallTargetWriteFailed)
        {
            return AgentDistributionFailureCategory.WriteOrFileSystemFailure;
        }

        return AgentDistributionFailureCategory.UnexpectedInternalFailure;
    }

    private static bool IsDriftOrLocalModification (AgentDistributionFailureCode code)
    {
        return code == AgentDistributionFailureCodes.InstallTargetDigestMismatch
            || code == AgentDistributionFailureCodes.InstallTargetManifestDigestMismatch
            || code == AgentDistributionFailureCodes.InstallTargetContentDigestMismatch
            || code == AgentDistributionFailureCodes.InstallTargetFrontmatterDigestMismatch
            || code == AgentDistributionFailureCodes.InstallTargetHostArtifactDigestMismatch
            || code == AgentDistributionFailureCodes.InstallTargetFileSetMismatch
            || code == AgentDistributionFailureCodes.InstallTargetOutdated
            || code == AgentDistributionFailureCodes.InstallTargetVersionAhead
            || code == AgentDistributionFailureCodes.InstallTargetLocalModification;
    }
}
