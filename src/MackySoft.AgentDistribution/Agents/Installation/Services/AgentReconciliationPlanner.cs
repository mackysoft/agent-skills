using MackySoft.AgentDistribution.Agents.Installation.Results;
using MackySoft.AgentDistribution.Agents.Installation.State;
using MackySoft.AgentDistribution.Agents.Installation.Targeting;
using MackySoft.AgentDistribution.Agents.Packaging;
using MackySoft.AgentDistribution.Digests;
using MackySoft.AgentDistribution.Shared;
using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Agents.Installation.Services;

internal sealed class AgentReconciliationPlanner
{
    private readonly AgentInstalledTargetInspector targetInspector;
    private readonly SkillDigestCalculator digestCalculator;

    public AgentReconciliationPlanner (AgentInstalledTargetInspector targetInspector, SkillDigestCalculator digestCalculator)
    {
        this.targetInspector = targetInspector ?? throw new ArgumentNullException(nameof(targetInspector));
        this.digestCalculator = digestCalculator ?? throw new ArgumentNullException(nameof(digestCalculator));
    }

    public async ValueTask<SkillOperationResult<IReadOnlyList<AgentReconciliationPlan>>> CreatePlansAsync (
        IReadOnlyList<CanonicalAgentPackage> packages,
        AgentResolvedTarget target,
        AgentReconciliationMode mode,
        bool force,
        bool printDiff,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(packages);
        ArgumentNullException.ThrowIfNull(target);
        cancellationToken.ThrowIfCancellationRequested();

        var plans = new List<AgentReconciliationPlan>(packages.Count);
        var artifactOwners = new Dictionary<PackageRelativePath, AgentName>(PackageRelativePath.PortableFileSystemComparer);
        foreach (var package in packages.OrderBy(static package => package.Manifest.AgentName.Value, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var artifactsResult = CreateArtifacts(package, target.HostId);
            if (!artifactsResult.IsSuccess)
            {
                return SkillOperationResult<IReadOnlyList<AgentReconciliationPlan>>.FailureResult(
                    artifactsResult.Failure!.Code,
                    artifactsResult.Failure.Message);
            }

            foreach (var artifact in artifactsResult.Value!)
            {
                if (artifactOwners.TryGetValue(artifact.RelativePath, out var owner))
                {
                    return SkillOperationResult<IReadOnlyList<AgentReconciliationPlan>>.FailureResult(
                        SkillFailureCodes.InputInvalid,
                        $"Selected agents '{owner.Value}' and '{package.Manifest.AgentName.Value}' produce the same host artifact path: {artifact.RelativePath}.");
                }

                artifactOwners.Add(artifact.RelativePath, package.Manifest.AgentName);
            }

            var stateResult = await targetInspector.InspectAsync(package.Manifest, target, cancellationToken).ConfigureAwait(false);
            if (!stateResult.IsSuccess)
            {
                return SkillOperationResult<IReadOnlyList<AgentReconciliationPlan>>.FailureResult(
                    stateResult.Failure!.Code,
                    stateResult.Failure.Message);
            }

            var targetState = stateResult.Value!;
            var actionKind = ResolveActionKind(targetState, mode, force);
            var diffsResult = await CreateDiffsAsync(target, artifactsResult.Value!, actionKind, printDiff, cancellationToken).ConfigureAwait(false);
            if (!diffsResult.IsSuccess)
            {
                return SkillOperationResult<IReadOnlyList<AgentReconciliationPlan>>.FailureResult(
                    diffsResult.Failure!.Code,
                    diffsResult.Failure.Message);
            }

            var action = new AgentReconcileAction(
                package.Manifest.AgentName,
                actionKind,
                targetState.Kind,
                targetState.Detail,
                printDiff ? diffsResult.Value! : null);
            var desiredState = new AgentInstallationState(
                AgentInstallationState.CurrentSchemaVersion,
                package.Manifest.BundleVersion,
                package.Manifest.CatalogId,
                target.HostId,
                package.Manifest.AgentName,
                package.Manifest.ManifestDigest,
                artifactsResult.Value!
                    .Select(static artifact => new AgentInstalledArtifact(artifact.RelativePath, artifact.Digest))
                    .ToArray());
            plans.Add(new AgentReconciliationPlan(package, targetState, action, artifactsResult.Value!, desiredState));
        }

        return SkillOperationResult<IReadOnlyList<AgentReconciliationPlan>>.Success(Array.AsReadOnly(plans.ToArray()));
    }

    private SkillOperationResult<IReadOnlyList<AgentPlannedArtifact>> CreateArtifacts (CanonicalAgentPackage package, HostKind hostId)
    {
        var packageFiles = package.Files.ToDictionary(static file => file.RelativePath);
        var artifacts = new List<AgentPlannedArtifact>();
        foreach (var manifestArtifact in package.Manifest.HostArtifacts.Where(artifact => artifact.HostId == hostId))
        {
            if (!packageFiles.TryGetValue(manifestArtifact.Path, out var packageFile))
            {
                return SkillOperationResult<IReadOnlyList<AgentPlannedArtifact>>.FailureResult(
                    SkillFailureCodes.ManifestInvalid,
                    $"Agent host artifact is missing or has an unsafe target-relative path: {manifestArtifact.Path}.");
            }

            artifacts.Add(new AgentPlannedArtifact(
                manifestArtifact.HostTargetRelativePath,
                packageFile.Content,
                digestCalculator.ComputeSingleFileDigest(manifestArtifact.HostTargetRelativePath, packageFile.Content)));
        }

        if (artifacts.Count == 0)
        {
            return SkillOperationResult<IReadOnlyList<AgentPlannedArtifact>>.FailureResult(
                SkillFailureCodes.HostUnsupported,
                $"Agent '{package.Manifest.AgentName.Value}' has no artifacts for host '{Vocabulary.GetText(hostId)}'.");
        }

        return SkillOperationResult<IReadOnlyList<AgentPlannedArtifact>>.Success(
            Array.AsReadOnly(artifacts.OrderBy(static artifact => artifact.RelativePath.Value, StringComparer.Ordinal).ToArray()));
    }

    private static AgentReconcileActionKind ResolveActionKind (
        AgentInstalledTargetState state,
        AgentReconciliationMode mode,
        bool force)
    {
        return state.Kind switch
        {
            AgentInstalledTargetStateKind.Missing => AgentReconcileActionKind.Created,
            AgentInstalledTargetStateKind.Current => AgentReconcileActionKind.NoOp,
            AgentInstalledTargetStateKind.CleanOutdated when mode == AgentReconciliationMode.Update || force => AgentReconcileActionKind.Updated,
            AgentInstalledTargetStateKind.CleanOutdated => AgentReconcileActionKind.BlockedManagedOverwrite,
            AgentInstalledTargetStateKind.LocallyModified when force => AgentReconcileActionKind.Updated,
            AgentInstalledTargetStateKind.LocallyModified => AgentReconcileActionKind.BlockedLocalModification,
            AgentInstalledTargetStateKind.Unmanaged => AgentReconcileActionKind.BlockedUnmanaged,
            AgentInstalledTargetStateKind.OtherCatalog => AgentReconcileActionKind.BlockedForeignCatalog,
            _ => AgentReconcileActionKind.BlockedInvalid,
        };
    }

    private static async ValueTask<SkillOperationResult<IReadOnlyList<AgentArtifactDiff>>> CreateDiffsAsync (
        AgentResolvedTarget target,
        IReadOnlyList<AgentPlannedArtifact> artifacts,
        AgentReconcileActionKind actionKind,
        bool printDiff,
        CancellationToken cancellationToken)
    {
        if (!printDiff)
        {
            return SkillOperationResult<IReadOnlyList<AgentArtifactDiff>>.Success(Array.Empty<AgentArtifactDiff>());
        }

        if (actionKind == AgentReconcileActionKind.NoOp)
        {
            return SkillOperationResult<IReadOnlyList<AgentArtifactDiff>>.Success(Array.Empty<AgentArtifactDiff>());
        }

        var diffs = new List<AgentArtifactDiff>(artifacts.Count);
        foreach (var artifact in artifacts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pathResult = AgentPathGuard.Validate(ContainedPath.Create(target.ArtifactRoot, artifact.RelativePath.RootRelativePath));
            if (!pathResult.IsSuccess)
            {
                return SkillOperationResult<IReadOnlyList<AgentArtifactDiff>>.FailureResult(pathResult.Failure!.Code, pathResult.Failure.Message);
            }

            string? beforeContent = null;
            if (File.Exists(pathResult.Value!.Value))
            {
                try
                {
                    beforeContent = await File.ReadAllTextAsync(pathResult.Value!.Value, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    return SkillOperationResult<IReadOnlyList<AgentArtifactDiff>>.FailureResult(
                        SkillFailureCodes.InstallTargetReadFailed,
                        $"Could not read agent artifact for diff: {exception.Message}");
                }
            }

            diffs.Add(new AgentArtifactDiff(artifact.RelativePath, beforeContent, artifact.Content));
        }

        return SkillOperationResult<IReadOnlyList<AgentArtifactDiff>>.Success(Array.AsReadOnly(diffs.ToArray()));
    }
}
