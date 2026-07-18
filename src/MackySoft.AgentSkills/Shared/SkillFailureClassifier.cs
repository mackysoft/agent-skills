namespace MackySoft.AgentSkills.Shared;

/// <summary> Classifies SKILL failure codes into product-neutral categories. </summary>
public static class SkillFailureClassifier
{
    /// <summary> Classifies one SKILL failure. </summary>
    /// <param name="failure"> The failure to classify. Must not be <see langword="null" />. </param>
    /// <returns> The product-neutral category for <paramref name="failure" />. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="failure" /> is <see langword="null" />. </exception>
    public static SkillFailureCategory Classify (SkillFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return Classify(failure.Code);
    }

    /// <summary> Classifies one SKILL failure code. </summary>
    /// <param name="code"> The failure code to classify. Unknown values are allowed. </param>
    /// <returns> The product-neutral category for <paramref name="code" />, or <see cref="SkillFailureCategory.UnexpectedInternalFailure" /> for unknown codes. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="code" /> is <see langword="null" />. </exception>
    public static SkillFailureCategory Classify (SkillFailureCode code)
    {
        ArgumentNullException.ThrowIfNull(code);
        if (code == SkillFailureCodes.InputInvalid)
        {
            return SkillFailureCategory.InvalidInput;
        }

        if (code == SkillFailureCodes.PathUnsafe)
        {
            return SkillFailureCategory.UnsafePath;
        }

        if (code == SkillFailureCodes.HostUnsupported)
        {
            return SkillFailureCategory.UnsupportedHost;
        }

        if (code == SkillFailureCodes.ScopeUnsupported)
        {
            return SkillFailureCategory.UnsupportedScope;
        }

        if (code == SkillFailureCodes.UserTargetUnavailable)
        {
            return SkillFailureCategory.UserTargetUnavailable;
        }

        if (code == SkillFailureCodes.ManifestInvalid)
        {
            return SkillFailureCategory.ManifestInvalid;
        }

        if (code == SkillFailureCodes.SourceInvalid
            || code == SkillFailureCodes.BundleVersionConflict)
        {
            return SkillFailureCategory.SourceInvalid;
        }

        if (code == SkillFailureCodes.BundleUpdateRequired)
        {
            return SkillFailureCategory.DriftOrLocalModification;
        }

        if (IsDriftOrLocalModification(code))
        {
            return SkillFailureCategory.DriftOrLocalModification;
        }

        if (code == SkillFailureCodes.InstallTargetRemovedFromCatalog)
        {
            return SkillFailureCategory.RemovedFromCatalog;
        }

        if (code == SkillFailureCodes.InstallTargetUnmanaged)
        {
            return SkillFailureCategory.UnmanagedTarget;
        }

        if (code == SkillFailureCodes.InstallTargetNameCollision)
        {
            return SkillFailureCategory.NameCollision;
        }

        if (code == SkillFailureCodes.InstallTargetHostConflict)
        {
            return SkillFailureCategory.HostConflict;
        }

        if (code == SkillFailureCodes.InstallTargetRootConflict)
        {
            return SkillFailureCategory.TargetRootConflict;
        }

        if (code == SkillFailureCodes.InstallTargetReadFailed)
        {
            return SkillFailureCategory.ReadFailure;
        }

        if (code == SkillFailureCodes.InstallTargetWriteFailed)
        {
            return SkillFailureCategory.WriteOrFileSystemFailure;
        }

        return SkillFailureCategory.UnexpectedInternalFailure;
    }

    private static bool IsDriftOrLocalModification (SkillFailureCode code)
    {
        return code == SkillFailureCodes.InstallTargetDigestMismatch
            || code == SkillFailureCodes.InstallTargetManifestDigestMismatch
            || code == SkillFailureCodes.InstallTargetContentDigestMismatch
            || code == SkillFailureCodes.InstallTargetFrontmatterDigestMismatch
            || code == SkillFailureCodes.InstallTargetHostArtifactDigestMismatch
            || code == SkillFailureCodes.InstallTargetFileSetMismatch
            || code == SkillFailureCodes.InstallTargetOutdated
            || code == SkillFailureCodes.InstallTargetVersionAhead
            || code == SkillFailureCodes.InstallTargetLocalModification;
    }
}
