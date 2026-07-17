using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Installation.State;

/// <summary> Represents the analyzed state of one installed SKILL target. </summary>
public sealed class SkillInstalledTargetState
{
    private SkillInstalledTargetState (
        SkillTargetStateKind kind,
        SkillFailure? failure,
        SkillInstalledTargetFileSet? fileSet,
        int? installedSkillBundleVersion,
        int? bundledSkillBundleVersion)
    {
        if (!IsAnalyzedStateKind(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "The target state kind is not produced by installed target analysis.");
        }

        ValidateFailure(kind, failure);
        ValidateFileSet(kind, fileSet);
        ValidateVersions(kind, installedSkillBundleVersion, bundledSkillBundleVersion);

        Kind = kind;
        Failure = failure;
        FileSet = fileSet;
        InstalledSkillBundleVersion = installedSkillBundleVersion;
        BundledSkillBundleVersion = bundledSkillBundleVersion;
    }

    internal static SkillInstalledTargetState Missing (int bundledSkillBundleVersion)
    {
        return new SkillInstalledTargetState(
            SkillTargetStateKind.Missing,
            failure: null,
            fileSet: null,
            installedSkillBundleVersion: null,
            bundledSkillBundleVersion);
    }

    internal static SkillInstalledTargetState Current (
        int installedSkillBundleVersion,
        int bundledSkillBundleVersion)
    {
        return new SkillInstalledTargetState(
            SkillTargetStateKind.Current,
            failure: null,
            fileSet: null,
            installedSkillBundleVersion,
            bundledSkillBundleVersion);
    }

    internal static SkillInstalledTargetState CleanOutdated (
        SkillFailure failure,
        int installedSkillBundleVersion,
        int bundledSkillBundleVersion)
    {
        return new SkillInstalledTargetState(
            SkillTargetStateKind.CleanOutdated,
            failure,
            fileSet: null,
            installedSkillBundleVersion,
            bundledSkillBundleVersion);
    }

    internal static SkillInstalledTargetState VersionAhead (
        SkillFailure failure,
        int installedSkillBundleVersion,
        int bundledSkillBundleVersion)
    {
        return new SkillInstalledTargetState(
            SkillTargetStateKind.VersionAhead,
            failure,
            fileSet: null,
            installedSkillBundleVersion,
            bundledSkillBundleVersion);
    }

    internal static SkillInstalledTargetState Blocking (
        SkillTargetStateKind kind,
        SkillFailure failure)
    {
        if (kind is not (SkillTargetStateKind.Unmanaged or SkillTargetStateKind.NameCollision or SkillTargetStateKind.HostConflict))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "The target state kind is not a blocking analyzer state.");
        }

        return new SkillInstalledTargetState(
            kind,
            failure,
            fileSet: null,
            installedSkillBundleVersion: null,
            bundledSkillBundleVersion: null);
    }

    internal static SkillInstalledTargetState Drift (
        SkillTargetStateKind kind,
        SkillFailure failure,
        int bundledSkillBundleVersion)
    {
        if (!SkillTargetStateClassifier.IsLocalModificationDrift(kind)
            || kind == SkillTargetStateKind.FileSetDrift)
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "The target state kind is not a drift state without file-set details.");
        }

        return new SkillInstalledTargetState(
            kind,
            failure,
            fileSet: null,
            installedSkillBundleVersion: null,
            bundledSkillBundleVersion);
    }

    internal static SkillInstalledTargetState FileSetDrift (
        SkillFailure failure,
        SkillInstalledTargetFileSet fileSet,
        int bundledSkillBundleVersion)
    {
        return new SkillInstalledTargetState(
            SkillTargetStateKind.FileSetDrift,
            failure,
            fileSet,
            installedSkillBundleVersion: null,
            bundledSkillBundleVersion);
    }

    /// <summary> Gets the target state kind. </summary>
    public SkillTargetStateKind Kind { get; }

    /// <summary> Gets the failure contract represented by this state, when applicable. </summary>
    public SkillFailure? Failure { get; }

    /// <summary> Gets the structured file-set drift details, when the state is file-set drift. </summary>
    public SkillInstalledTargetFileSet? FileSet { get; }

    /// <summary> Gets the installed manifest SKILL bundle version, when available. </summary>
    public int? InstalledSkillBundleVersion { get; }

    /// <summary> Gets the bundled canonical package SKILL bundle version, when available. </summary>
    public int? BundledSkillBundleVersion { get; }

    private static bool IsAnalyzedStateKind (SkillTargetStateKind kind)
    {
        return kind is SkillTargetStateKind.Missing
            or SkillTargetStateKind.Current
            or SkillTargetStateKind.CleanOutdated
            or SkillTargetStateKind.LocalModified
            or SkillTargetStateKind.Unmanaged
            or SkillTargetStateKind.ManifestDrift
            or SkillTargetStateKind.CommonContentDrift
            or SkillTargetStateKind.FrontmatterDrift
            or SkillTargetStateKind.HostArtifactDrift
            or SkillTargetStateKind.FileSetDrift
            or SkillTargetStateKind.NameCollision
            or SkillTargetStateKind.HostConflict
            or SkillTargetStateKind.VersionAhead;
    }

    private static void ValidateFailure (
        SkillTargetStateKind kind,
        SkillFailure? failure)
    {
        if (kind is SkillTargetStateKind.Missing or SkillTargetStateKind.Current)
        {
            if (failure is not null)
            {
                throw new ArgumentException("Missing and current target states must not contain a failure.", nameof(failure));
            }

            return;
        }

        ArgumentNullException.ThrowIfNull(failure);
        if (SkillTargetStateClassifier.TryResolveDriftKind(failure.Code, out var driftKind))
        {
            if (driftKind != kind)
            {
                throw new ArgumentException("Failure code does not match the target state kind.", nameof(failure));
            }

            return;
        }

        if (!SkillTargetStateClassifier.TryResolveNonDriftFailureKind(failure.Code, out var failureKind)
            || failureKind != kind)
        {
            throw new ArgumentException("Failure code does not match the target state kind.", nameof(failure));
        }
    }

    private static void ValidateFileSet (
        SkillTargetStateKind kind,
        SkillInstalledTargetFileSet? fileSet)
    {
        if ((kind == SkillTargetStateKind.FileSetDrift) != (fileSet is not null))
        {
            throw new ArgumentException("File-set details must be supplied only for file-set drift.", nameof(fileSet));
        }

        if (fileSet is not null
            && fileSet.MissingFiles.Count == 0
            && fileSet.ExtraFiles.Count == 0
            && fileSet.ExtraDirectories.Count == 0)
        {
            throw new ArgumentException("File-set drift must contain at least one missing or extra path.", nameof(fileSet));
        }
    }

    private static void ValidateVersions (
        SkillTargetStateKind kind,
        int? installedSkillBundleVersion,
        int? bundledSkillBundleVersion)
    {
        if (installedSkillBundleVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(installedSkillBundleVersion),
                installedSkillBundleVersion,
                "Installed SKILL bundle version must be positive when supplied.");
        }

        if (bundledSkillBundleVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(bundledSkillBundleVersion),
                bundledSkillBundleVersion,
                "Bundled SKILL bundle version must be positive when supplied.");
        }

        if (kind == SkillTargetStateKind.Missing)
        {
            if (installedSkillBundleVersion is not null || bundledSkillBundleVersion is null)
            {
                throw new ArgumentException("A missing target must contain only the bundled SKILL bundle version.");
            }

            return;
        }

        if (kind == SkillTargetStateKind.Current)
        {
            if (installedSkillBundleVersion is null || bundledSkillBundleVersion is null)
            {
                throw new ArgumentException("A current target must contain installed and bundled SKILL bundle versions.");
            }

            if (installedSkillBundleVersion != bundledSkillBundleVersion)
            {
                throw new ArgumentException("A current target must have matching installed and bundled SKILL bundle versions.");
            }

            return;
        }

        if (kind is SkillTargetStateKind.CleanOutdated or SkillTargetStateKind.VersionAhead)
        {
            if (installedSkillBundleVersion is null || bundledSkillBundleVersion is null)
            {
                throw new ArgumentException("Version comparison states must contain installed and bundled SKILL bundle versions.");
            }

            if (kind == SkillTargetStateKind.VersionAhead
                && installedSkillBundleVersion <= bundledSkillBundleVersion)
            {
                throw new ArgumentException("A version-ahead target must have a newer installed SKILL bundle version.");
            }

            if (kind == SkillTargetStateKind.CleanOutdated
                && installedSkillBundleVersion > bundledSkillBundleVersion)
            {
                throw new ArgumentException("A clean-outdated target must not have a newer installed SKILL bundle version.");
            }

            return;
        }

        if (kind is SkillTargetStateKind.Unmanaged or SkillTargetStateKind.NameCollision or SkillTargetStateKind.HostConflict)
        {
            if (installedSkillBundleVersion is not null || bundledSkillBundleVersion is not null)
            {
                throw new ArgumentException("An unmanaged or conflicting target state must not contain SKILL bundle versions.");
            }

            return;
        }

        if (installedSkillBundleVersion is not null || bundledSkillBundleVersion is null)
        {
            throw new ArgumentException("A managed drift state must contain only the bundled SKILL bundle version.");
        }
    }
}
