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
        if (currentFailure.Code == SkillFailureCodes.InstallTargetUnmanaged)
        {
            return Success(SkillInstalledTargetStateKind.Unmanaged, currentFailure);
        }

        if (currentFailure.Code == SkillFailureCodes.InstallTargetNameCollision)
        {
            return Success(SkillInstalledTargetStateKind.NameCollision, currentFailure);
        }

        if (currentFailure.Code == SkillFailureCodes.InstallTargetHostConflict)
        {
            return Success(SkillInstalledTargetStateKind.HostConflict, currentFailure);
        }

        if (!IsDriftFailure(currentFailure.Code))
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
        if (integrityFailure.Code == SkillFailureCodes.InstallTargetUnmanaged)
        {
            return Success(SkillInstalledTargetStateKind.Unmanaged, integrityFailure);
        }

        if (integrityFailure.Code == SkillFailureCodes.InstallTargetNameCollision)
        {
            return Success(SkillInstalledTargetStateKind.NameCollision, integrityFailure);
        }

        if (integrityFailure.Code == SkillFailureCodes.InstallTargetHostConflict)
        {
            return Success(SkillInstalledTargetStateKind.HostConflict, integrityFailure);
        }

        if (!IsDriftFailure(integrityFailure.Code))
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
        var stateKind = ResolveStateKind(selectedFailure.Code);
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
        if (currentFailure.Code == SkillFailureCodes.InstallTargetFileSetMismatch)
        {
            return currentFailure;
        }

        if (currentFailure.Code == SkillFailureCodes.InstallTargetHostArtifactDigestMismatch
            && integrityFailure.Code == SkillFailureCodes.InstallTargetContentDigestMismatch)
        {
            return currentFailure;
        }

        return integrityFailure.Code == SkillFailureCodes.InstallTargetDigestMismatch
            ? SkillFailure.Create(SkillFailureCodes.InstallTargetLocalModification, integrityFailure.Message)
            : integrityFailure;
    }

    private static ValueTask<SkillOperationResult<SkillInstalledFileSetVerificationResult>> ReadCurrentFileSetDriftAsync (
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
            return ValueTask.FromResult(SkillOperationResult<SkillInstalledFileSetVerificationResult>.FailureResult(
                entriesResult.Failure!.Code,
                entriesResult.Failure.Message));
        }

        var hostArtifact = package.Manifest.HostArtifacts.SingleOrDefault(artifact => string.Equals(artifact.Host, host, StringComparison.Ordinal));
        if (hostArtifact is null)
        {
            return ValueTask.FromResult(SkillOperationResult<SkillInstalledFileSetVerificationResult>.FailureResult(
                SkillFailureCodes.ManifestInvalid,
                $"Manifest does not contain host artifact '{host}'."));
        }

        var hostArtifactPath = hostArtifact.Path;
        var requiredPaths = package.Files
            .Select(static file => file.RelativePath)
            .Where(path => string.Equals(path, "agent-skill.json", StringComparison.Ordinal)
                || string.Equals(path, "SKILL.md", StringComparison.Ordinal)
                || path.StartsWith("references/", StringComparison.Ordinal)
                || (!string.IsNullOrWhiteSpace(hostArtifactPath) && string.Equals(path, hostArtifactPath, StringComparison.Ordinal)))
            .ToArray();

        return ValueTask.FromResult(SkillInstalledFileSetVerifier.VerifyInstalledEntries(
            skillDirectory,
            requiredPaths,
            Array.Empty<string>(),
            entriesResult.Value!,
            cancellationToken));
    }

    private static SkillInstalledTargetStateKind ResolveStateKind (SkillFailureCode code)
    {
        if (code == SkillFailureCodes.InstallTargetManifestDigestMismatch)
        {
            return SkillInstalledTargetStateKind.ManifestDrift;
        }

        if (code == SkillFailureCodes.InstallTargetContentDigestMismatch)
        {
            return SkillInstalledTargetStateKind.CommonContentDrift;
        }

        if (code == SkillFailureCodes.InstallTargetFrontmatterDigestMismatch)
        {
            return SkillInstalledTargetStateKind.FrontmatterDrift;
        }

        if (code == SkillFailureCodes.InstallTargetHostArtifactDigestMismatch)
        {
            return SkillInstalledTargetStateKind.HostArtifactDrift;
        }

        if (code == SkillFailureCodes.InstallTargetFileSetMismatch)
        {
            return SkillInstalledTargetStateKind.FileSetDrift;
        }

        return SkillInstalledTargetStateKind.LocalModified;
    }

    private static bool IsDriftFailure (SkillFailureCode code)
    {
        return code == SkillFailureCodes.InstallTargetDigestMismatch
            || code == SkillFailureCodes.InstallTargetManifestDigestMismatch
            || code == SkillFailureCodes.InstallTargetContentDigestMismatch
            || code == SkillFailureCodes.InstallTargetFrontmatterDigestMismatch
            || code == SkillFailureCodes.InstallTargetHostArtifactDigestMismatch
            || code == SkillFailureCodes.InstallTargetFileSetMismatch
            || code == SkillFailureCodes.InstallTargetLocalModification;
    }

    private static SkillOperationResult<SkillInstalledTargetState> Success (
        SkillInstalledTargetStateKind kind,
        SkillFailure? failure = null,
        SkillInstalledFileSetVerificationResult? fileSet = null)
    {
        return SkillOperationResult<SkillInstalledTargetState>.Success(new SkillInstalledTargetState(kind, failure, fileSet));
    }
}
