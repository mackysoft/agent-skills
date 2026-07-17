using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Installation.Results;

/// <summary> Represents the analyzed target state projected onto an operation action. </summary>
public sealed class SkillActionTargetState
{
    /// <summary> Initializes one analyzed action target state. </summary>
    internal SkillActionTargetState (
        SkillTargetStateKind kind,
        SkillFailureCode? code,
        string? message,
        SkillActionTargetFileSet? fileSet,
        int? installedSkillBundleVersion,
        int? bundledSkillBundleVersion)
    {
        Kind = SkillActionContractGuard.ValidateEnum(kind, nameof(kind));
        ValidateFailure(Kind, code, message);
        ValidateFileSet(Kind, fileSet);
        ValidateVersions(Kind, installedSkillBundleVersion, bundledSkillBundleVersion);

        Code = code;
        Message = message;
        FileSet = fileSet;
        InstalledSkillBundleVersion = installedSkillBundleVersion;
        BundledSkillBundleVersion = bundledSkillBundleVersion;
    }

    /// <summary> Gets the analyzed target state kind. </summary>
    public SkillTargetStateKind Kind { get; }

    /// <summary> Gets the failure code represented by this state, when applicable. </summary>
    public SkillFailureCode? Code { get; }

    /// <summary> Gets the failure message represented by this state, when applicable. </summary>
    public string? Message { get; }

    /// <summary> Gets file-set drift details when available for <see cref="SkillTargetStateKind.FileSetDrift" />. </summary>
    public SkillActionTargetFileSet? FileSet { get; }

    /// <summary> Gets the installed target SKILL bundle version, when available. </summary>
    public int? InstalledSkillBundleVersion { get; }

    /// <summary> Gets the bundled canonical package SKILL bundle version, when available. </summary>
    public int? BundledSkillBundleVersion { get; }

    private static void ValidateFailure (
        SkillTargetStateKind kind,
        SkillFailureCode? code,
        string? message)
    {
        if (kind is SkillTargetStateKind.Missing or SkillTargetStateKind.Current)
        {
            if (code is not null || message is not null)
            {
                throw new ArgumentException("Missing and current target states must not contain a failure.", nameof(code));
            }

            return;
        }

        if (code is null)
        {
            throw new ArgumentException("A non-current target state requires a valid failure code.", nameof(code));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        if (kind == SkillTargetStateKind.ManifestDrift && code == SkillFailureCodes.ManifestInvalid)
        {
            return;
        }

        if (SkillTargetStateClassifier.TryResolveDriftKind(code, out var driftKind))
        {
            if (driftKind != kind)
            {
                throw new ArgumentException("The failure code does not match the target state kind.", nameof(code));
            }

            return;
        }

        if (!SkillTargetStateClassifier.TryResolveNonDriftFailureKind(code, out var failureKind)
            || failureKind != kind)
        {
            throw new ArgumentException("The failure code does not match the target state kind.", nameof(code));
        }
    }

    private static void ValidateFileSet (
        SkillTargetStateKind kind,
        SkillActionTargetFileSet? fileSet)
    {
        if (kind != SkillTargetStateKind.FileSetDrift && fileSet is not null)
        {
            throw new ArgumentException("File-set details may be supplied only for file-set drift.", nameof(fileSet));
        }

        if (fileSet is not null
            && fileSet.MissingFiles.Count == 0
            && fileSet.ExtraFiles.Count == 0
            && fileSet.ExtraDirectories.Count == 0)
        {
            throw new ArgumentException("File-set drift details must contain at least one missing or extra path.", nameof(fileSet));
        }
    }

    private static void ValidateVersions (
        SkillTargetStateKind kind,
        int? installedSkillBundleVersion,
        int? bundledSkillBundleVersion)
    {
        SkillActionContractGuard.ValidateVersion(installedSkillBundleVersion, nameof(installedSkillBundleVersion));
        SkillActionContractGuard.ValidateVersion(bundledSkillBundleVersion, nameof(bundledSkillBundleVersion));

        switch (kind)
        {
            case SkillTargetStateKind.Missing:
                if (installedSkillBundleVersion is not null || bundledSkillBundleVersion is null)
                {
                    throw new ArgumentException("A missing target must contain only the bundled SKILL bundle version.", nameof(bundledSkillBundleVersion));
                }

                return;
            case SkillTargetStateKind.Current:
                if (installedSkillBundleVersion is null
                    || bundledSkillBundleVersion is null
                    || installedSkillBundleVersion != bundledSkillBundleVersion)
                {
                    throw new ArgumentException("A current target must contain matching installed and bundled SKILL bundle versions.", nameof(installedSkillBundleVersion));
                }

                return;
            case SkillTargetStateKind.CleanOutdated:
                ValidateComparedVersions(installedSkillBundleVersion, bundledSkillBundleVersion, versionAhead: false);
                return;
            case SkillTargetStateKind.VersionAhead:
                ValidateComparedVersions(installedSkillBundleVersion, bundledSkillBundleVersion, versionAhead: true);
                return;
            case SkillTargetStateKind.RemovedFromCatalog:
                if (installedSkillBundleVersion is null || bundledSkillBundleVersion is not null)
                {
                    throw new ArgumentException("A removed-from-catalog target must contain only the installed SKILL bundle version.", nameof(installedSkillBundleVersion));
                }

                return;
            case SkillTargetStateKind.Unmanaged:
            case SkillTargetStateKind.NameCollision:
            case SkillTargetStateKind.HostConflict:
                if (installedSkillBundleVersion is not null || bundledSkillBundleVersion is not null)
                {
                    throw new ArgumentException("An unmanaged or conflicting target state must not contain SKILL bundle versions.", nameof(installedSkillBundleVersion));
                }

                return;
            default:
                ValidateDriftVersions(kind, installedSkillBundleVersion, bundledSkillBundleVersion);
                return;
        }
    }

    private static void ValidateDriftVersions (
        SkillTargetStateKind kind,
        int? installedSkillBundleVersion,
        int? bundledSkillBundleVersion)
    {
        if (!SkillTargetStateClassifier.IsLocalModificationDrift(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported action target state kind.");
        }

        if (installedSkillBundleVersion is not null && bundledSkillBundleVersion is not null)
        {
            throw new ArgumentException("A drift target state must not contain both installed and bundled SKILL bundle versions.", nameof(installedSkillBundleVersion));
        }

        if (kind != SkillTargetStateKind.ManifestDrift
            && installedSkillBundleVersion is null
            && bundledSkillBundleVersion is null)
        {
            throw new ArgumentException("A drift target state requires either an installed or bundled SKILL bundle version.", nameof(installedSkillBundleVersion));
        }
    }

    private static void ValidateComparedVersions (
        int? installedSkillBundleVersion,
        int? bundledSkillBundleVersion,
        bool versionAhead)
    {
        if (installedSkillBundleVersion is null || bundledSkillBundleVersion is null)
        {
            throw new ArgumentException("A version-comparison target state requires installed and bundled SKILL bundle versions.", nameof(installedSkillBundleVersion));
        }

        if (versionAhead == (installedSkillBundleVersion <= bundledSkillBundleVersion))
        {
            throw new ArgumentException("The installed and bundled SKILL bundle versions do not match the target state kind.", nameof(installedSkillBundleVersion));
        }
    }
}
