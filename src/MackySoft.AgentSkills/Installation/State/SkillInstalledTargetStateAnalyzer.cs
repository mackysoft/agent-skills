using MackySoft.AgentSkills.Installation.Validation;
using MackySoft.AgentSkills.Packaging.Canonical;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Installation.State;

/// <summary> Classifies an installed SKILL target before write or delete operations. </summary>
public sealed class SkillInstalledTargetStateAnalyzer
{
    private readonly SkillInstalledPackageValidator installedPackageValidator;
    private readonly SkillInstalledPackageIntegrityVerifier installedPackageIntegrityVerifier;

    /// <summary> Initializes a new instance of the <see cref="SkillInstalledTargetStateAnalyzer" /> class. </summary>
    /// <param name="installedPackageValidator"> The current canonical package validator. </param>
    /// <param name="installedPackageIntegrityVerifier"> The installed package integrity verifier. </param>
    public SkillInstalledTargetStateAnalyzer (
        SkillInstalledPackageValidator installedPackageValidator,
        SkillInstalledPackageIntegrityVerifier installedPackageIntegrityVerifier)
    {
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
        string host,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentException.ThrowIfNullOrWhiteSpace(skillDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        cancellationToken.ThrowIfCancellationRequested();

        if (!Directory.Exists(skillDirectory))
        {
            return SkillOperationResult<SkillInstalledTargetState>.Success(new SkillInstalledTargetState(SkillInstalledTargetStateKind.Missing));
        }

        var currentResult = await installedPackageValidator.ValidateAsync(package, skillDirectory, host, cancellationToken).ConfigureAwait(false);
        if (currentResult.IsSuccess)
        {
            return SkillOperationResult<SkillInstalledTargetState>.Success(new SkillInstalledTargetState(SkillInstalledTargetStateKind.Current));
        }

        var currentFailure = currentResult.Failure!;
        if (TryResolveNonDriftStateKind(currentFailure.Code, out var currentNonDriftKind))
        {
            return Success(currentNonDriftKind, currentFailure);
        }

        if (!SkillInstalledTargetStateKindExtensions.TryResolveDriftKind(currentFailure.Code, out _))
        {
            return SkillOperationResult<SkillInstalledTargetState>.FailureResult(currentFailure.Code, currentFailure.Message);
        }

        var integrityResult = await installedPackageIntegrityVerifier.VerifyAsync(skillDirectory, host, cancellationToken).ConfigureAwait(false);
        if (integrityResult.IsSuccess)
        {
            return Success(
                SkillInstalledTargetStateKind.CleanOutdated,
                SkillFailure.Create(
                    SkillFailureCodes.InstallTargetOutdated,
                    $"Installed SKILL package is clean but older than the canonical package: {package.Manifest.SkillName}"));
        }

        var integrityFailure = integrityResult.Failure!;
        if (TryResolveNonDriftStateKind(integrityFailure.Code, out var integrityNonDriftKind))
        {
            return Success(integrityNonDriftKind, integrityFailure);
        }

        if (!SkillInstalledTargetStateKindExtensions.TryResolveDriftKind(integrityFailure.Code, out _))
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
        string host,
        SkillFailure currentFailure,
        SkillFailure integrityFailure,
        CancellationToken cancellationToken)
    {
        var selectedFailure = SelectDriftFailure(currentFailure, integrityFailure);
        if (!SkillInstalledTargetStateKindExtensions.TryResolveDriftKind(selectedFailure.Code, out var stateKind))
        {
            return SkillOperationResult<SkillInstalledTargetState>.FailureResult(selectedFailure.Code, selectedFailure.Message);
        }

        if (stateKind == SkillInstalledTargetStateKind.FileSetDrift)
        {
            var fileSetResult = await ReadCurrentFileSetDriftAsync(package, skillDirectory, host, cancellationToken).ConfigureAwait(false);
            if (!fileSetResult.IsSuccess)
            {
                return SkillOperationResult<SkillInstalledTargetState>.FailureResult(fileSetResult.Failure!.Code, fileSetResult.Failure.Message);
            }

            return Success(stateKind, selectedFailure, fileSetResult.Value);
        }

        return Success(stateKind, selectedFailure);
    }

    private static SkillFailure SelectDriftFailure (
        SkillFailure currentFailure,
        SkillFailure integrityFailure)
    {
        var normalizedIntegrityFailure = integrityFailure.Code == SkillFailureCodes.InstallTargetDigestMismatch
            ? SkillFailure.Create(SkillFailureCodes.InstallTargetLocalModification, integrityFailure.Message)
            : integrityFailure;
        if (!SkillInstalledTargetStateKindExtensions.TryResolveDriftKind(currentFailure.Code, out var currentKind))
        {
            return normalizedIntegrityFailure;
        }

        if (!SkillInstalledTargetStateKindExtensions.TryResolveDriftKind(normalizedIntegrityFailure.Code, out var integrityKind))
        {
            return currentFailure;
        }

        return currentKind.GetDriftPriority() <= integrityKind.GetDriftPriority()
            ? currentFailure
            : normalizedIntegrityFailure;
    }

    private static bool TryResolveNonDriftStateKind (
        SkillFailureCode code,
        out SkillInstalledTargetStateKind kind)
    {
        if (code == SkillFailureCodes.InstallTargetUnmanaged)
        {
            kind = SkillInstalledTargetStateKind.Unmanaged;
            return true;
        }

        if (code == SkillFailureCodes.InstallTargetNameCollision)
        {
            kind = SkillInstalledTargetStateKind.NameCollision;
            return true;
        }

        if (code == SkillFailureCodes.InstallTargetHostConflict)
        {
            kind = SkillInstalledTargetStateKind.HostConflict;
            return true;
        }

        kind = default;
        return false;
    }

    private static ValueTask<SkillOperationResult<SkillInstalledTargetFileSet>> ReadCurrentFileSetDriftAsync (
        CanonicalSkillPackage package,
        string skillDirectory,
        string host,
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

    private static SkillOperationResult<SkillInstalledTargetState> Success (
        SkillInstalledTargetStateKind kind,
        SkillFailure? failure = null,
        SkillInstalledTargetFileSet? fileSet = null)
    {
        return SkillOperationResult<SkillInstalledTargetState>.Success(new SkillInstalledTargetState(kind, failure, fileSet));
    }
}
