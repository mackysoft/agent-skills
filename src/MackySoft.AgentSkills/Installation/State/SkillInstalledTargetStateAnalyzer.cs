using MackySoft.AgentSkills.Hosts.Contracts;
using MackySoft.AgentSkills.Installation.Validation;
using MackySoft.AgentSkills.Packaging.Canonical;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Installation.State;

/// <summary> Classifies an installed SKILL target before write or delete operations. </summary>
public sealed class SkillInstalledTargetStateAnalyzer
{
    private readonly SkillInstalledManifestReader installedManifestReader;
    private readonly SkillInstalledPackageValidator installedPackageValidator;
    private readonly SkillInstalledPackageIntegrityVerifier installedPackageIntegrityVerifier;

    /// <summary> Initializes a new instance of the <see cref="SkillInstalledTargetStateAnalyzer" /> class. </summary>
    /// <param name="installedManifestReader"> The installed manifest reader used to establish catalog ownership. </param>
    /// <param name="installedPackageValidator"> The current canonical package validator. </param>
    /// <param name="installedPackageIntegrityVerifier"> The installed package integrity verifier. </param>
    public SkillInstalledTargetStateAnalyzer (
        SkillInstalledManifestReader installedManifestReader,
        SkillInstalledPackageValidator installedPackageValidator,
        SkillInstalledPackageIntegrityVerifier installedPackageIntegrityVerifier)
    {
        this.installedManifestReader = installedManifestReader ?? throw new ArgumentNullException(nameof(installedManifestReader));
        this.installedPackageValidator = installedPackageValidator ?? throw new ArgumentNullException(nameof(installedPackageValidator));
        this.installedPackageIntegrityVerifier = installedPackageIntegrityVerifier ?? throw new ArgumentNullException(nameof(installedPackageIntegrityVerifier));
    }

    /// <summary> Analyzes one target directory against the current canonical package and requested host. </summary>
    /// <param name="package"> The canonical package. </param>
    /// <param name="skillDirectory"> The target skill directory. </param>
    /// <param name="host"> The requested host. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The target state or a hard safety failure. </returns>
    public async ValueTask<SkillOperationResult<SkillInstalledTargetState>> AnalyzeAsync (
        CanonicalSkillPackage package,
        string skillDirectory,
        SkillHostKind host,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentException.ThrowIfNullOrWhiteSpace(skillDirectory);
        cancellationToken.ThrowIfCancellationRequested();

        if (!Directory.Exists(skillDirectory))
        {
            return SkillOperationResult<SkillInstalledTargetState>.Success(
                SkillInstalledTargetState.Missing(package.Manifest.SkillBundleVersion));
        }

        var currentResult = await installedPackageValidator.ValidateAsync(package, skillDirectory, host, cancellationToken).ConfigureAwait(false);
        if (currentResult.IsSuccess)
        {
            return SkillOperationResult<SkillInstalledTargetState>.Success(
                SkillInstalledTargetState.Current(
                    currentResult.Value!.SkillBundleVersion,
                    package.Manifest.SkillBundleVersion));
        }

        var installedManifestResult = await installedManifestReader
            .ReadRequiredAsync(skillDirectory, cancellationToken)
            .ConfigureAwait(false);
        if (installedManifestResult.IsSuccess
            && installedManifestResult.Value!.Manifest.CatalogId != package.Manifest.CatalogId)
        {
            return SkillOperationResult<SkillInstalledTargetState>.Success(
                SkillInstalledTargetState.Blocking(
                    SkillTargetStateKind.Unmanaged,
                    SkillFailure.Create(
                        SkillFailureCodes.InstallTargetUnmanaged,
                        $"Installed SKILL belongs to another catalog: {installedManifestResult.Value.Manifest.CatalogId.Value}")));
        }

        var currentFailure = currentResult.Failure!;
        if (SkillTargetStateClassifier.TryResolveBlockingKind(currentFailure.Code, out var currentNonDriftKind))
        {
            return SkillOperationResult<SkillInstalledTargetState>.Success(
                SkillInstalledTargetState.Blocking(currentNonDriftKind, currentFailure));
        }

        if (!SkillTargetStateClassifier.TryResolveDriftKind(currentFailure.Code, out _))
        {
            return SkillOperationResult<SkillInstalledTargetState>.FailureResult(currentFailure.Code, currentFailure.Message);
        }

        var integrityResult = await installedPackageIntegrityVerifier.VerifyAsync(skillDirectory, host, cancellationToken).ConfigureAwait(false);
        if (integrityResult.IsSuccess)
        {
            var installedManifest = integrityResult.Value!;
            var kind = ResolveCleanManagedMismatchKind(installedManifest.SkillBundleVersion, package.Manifest.SkillBundleVersion);
            var failure = CreateCleanManagedMismatchFailure(
                package.Manifest.SkillName.Value,
                installedManifest.SkillBundleVersion,
                package.Manifest.SkillBundleVersion);
            var state = kind == SkillTargetStateKind.VersionAhead
                ? SkillInstalledTargetState.VersionAhead(
                    failure,
                    installedManifest.SkillBundleVersion,
                    package.Manifest.SkillBundleVersion)
                : SkillInstalledTargetState.CleanOutdated(
                    failure,
                    installedManifest.SkillBundleVersion,
                    package.Manifest.SkillBundleVersion);
            return SkillOperationResult<SkillInstalledTargetState>.Success(state);
        }

        var integrityFailure = integrityResult.Failure!;
        if (SkillTargetStateClassifier.TryResolveBlockingKind(integrityFailure.Code, out var integrityNonDriftKind))
        {
            return SkillOperationResult<SkillInstalledTargetState>.Success(
                SkillInstalledTargetState.Blocking(integrityNonDriftKind, integrityFailure));
        }

        if (!SkillTargetStateClassifier.TryResolveDriftKind(integrityFailure.Code, out _))
        {
            return SkillOperationResult<SkillInstalledTargetState>.FailureResult(integrityFailure.Code, integrityFailure.Message);
        }

        return await CreateDriftStateAsync(
                package,
                skillDirectory,
                host,
                currentFailure,
                integrityFailure,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask<SkillOperationResult<SkillInstalledTargetState>> CreateDriftStateAsync (
        CanonicalSkillPackage package,
        string skillDirectory,
        SkillHostKind host,
        SkillFailure currentFailure,
        SkillFailure integrityFailure,
        CancellationToken cancellationToken)
    {
        var selectedFailure = SelectDriftFailure(currentFailure, integrityFailure);
        if (!SkillTargetStateClassifier.TryResolveDriftKind(selectedFailure.Code, out var stateKind))
        {
            return SkillOperationResult<SkillInstalledTargetState>.FailureResult(selectedFailure.Code, selectedFailure.Message);
        }

        if (stateKind == SkillTargetStateKind.FileSetDrift)
        {
            var fileSetResult = await ReadCurrentFileSetDriftAsync(package, skillDirectory, host, cancellationToken).ConfigureAwait(false);
            if (!fileSetResult.IsSuccess)
            {
                return SkillOperationResult<SkillInstalledTargetState>.FailureResult(fileSetResult.Failure!.Code, fileSetResult.Failure.Message);
            }

            return SkillOperationResult<SkillInstalledTargetState>.Success(
                SkillInstalledTargetState.FileSetDrift(
                    selectedFailure,
                    fileSetResult.Value!,
                    package.Manifest.SkillBundleVersion));
        }

        return SkillOperationResult<SkillInstalledTargetState>.Success(
            SkillInstalledTargetState.Drift(
                stateKind,
                selectedFailure,
                package.Manifest.SkillBundleVersion));
    }

    private static SkillTargetStateKind ResolveCleanManagedMismatchKind (
        int installedSkillBundleVersion,
        int bundledSkillBundleVersion)
    {
        return installedSkillBundleVersion > bundledSkillBundleVersion
            ? SkillTargetStateKind.VersionAhead
            : SkillTargetStateKind.CleanOutdated;
    }

    private static SkillFailure CreateCleanManagedMismatchFailure (
        string skillName,
        int installedSkillBundleVersion,
        int bundledSkillBundleVersion)
    {
        if (installedSkillBundleVersion > bundledSkillBundleVersion)
        {
            return SkillFailure.Create(
                SkillFailureCodes.InstallTargetVersionAhead,
                $"Installed SKILL package was generated from a newer skillBundleVersion for '{skillName}': installed {installedSkillBundleVersion}, bundled {bundledSkillBundleVersion}.");
        }

        if (installedSkillBundleVersion == bundledSkillBundleVersion)
        {
            return SkillFailure.Create(
                SkillFailureCodes.InstallTargetOutdated,
                $"Installed SKILL package is clean but differs from the bundled package at skillBundleVersion {bundledSkillBundleVersion}: {skillName}");
        }

        return SkillFailure.Create(
            SkillFailureCodes.InstallTargetOutdated,
            $"Installed SKILL package is clean but older than the bundled package for '{skillName}': installed {installedSkillBundleVersion}, bundled {bundledSkillBundleVersion}.");
    }

    private static SkillFailure SelectDriftFailure (
        SkillFailure currentFailure,
        SkillFailure integrityFailure)
    {
        var normalizedIntegrityFailure = integrityFailure.Code == SkillFailureCodes.InstallTargetDigestMismatch
            ? SkillFailure.Create(SkillFailureCodes.InstallTargetLocalModification, integrityFailure.Message)
            : integrityFailure;
        if (!SkillTargetStateClassifier.TryResolveDriftKind(currentFailure.Code, out var currentKind))
        {
            return normalizedIntegrityFailure;
        }

        if (!SkillTargetStateClassifier.TryResolveDriftKind(normalizedIntegrityFailure.Code, out var integrityKind))
        {
            return currentFailure;
        }

        return SkillTargetStateClassifier.GetDriftPriority(currentKind) <= SkillTargetStateClassifier.GetDriftPriority(integrityKind)
            ? currentFailure
            : normalizedIntegrityFailure;
    }

    private static ValueTask<SkillOperationResult<SkillInstalledTargetFileSet>> ReadCurrentFileSetDriftAsync (
        CanonicalSkillPackage package,
        string skillDirectory,
        SkillHostKind host,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(package);

        var entriesResult = SkillInstalledFileSetVerifier.ReadInstalledEntries(skillDirectory, cancellationToken);
        if (!entriesResult.IsSuccess)
        {
            return ValueTask.FromResult(SkillOperationResult<SkillInstalledTargetFileSet>.FailureResult(
                entriesResult.Failure!.Code,
                entriesResult.Failure.Message));
        }

        var requiredPathsResult = SkillManagedFileSetPaths.CreateMaterializedRequiredPaths(package, host);
        if (!requiredPathsResult.IsSuccess)
        {
            return ValueTask.FromResult(SkillOperationResult<SkillInstalledTargetFileSet>.FailureResult(
                requiredPathsResult.Failure!.Code,
                requiredPathsResult.Failure.Message));
        }

        var fileSetResult = SkillInstalledFileSetVerifier.VerifyInstalledEntries(
            skillDirectory,
            requiredPathsResult.Value!,
            Array.Empty<string>(),
            entriesResult.Value!,
            cancellationToken);
        if (!fileSetResult.IsSuccess)
        {
            return ValueTask.FromResult(SkillOperationResult<SkillInstalledTargetFileSet>.FailureResult(
                fileSetResult.Failure!.Code,
                fileSetResult.Failure.Message));
        }

        var fileSet = fileSetResult.Value!;
        return ValueTask.FromResult(SkillOperationResult<SkillInstalledTargetFileSet>.Success(new SkillInstalledTargetFileSet(
            fileSet.MissingFiles,
            fileSet.ExtraFiles,
            fileSet.ExtraDirectories)));
    }

}
