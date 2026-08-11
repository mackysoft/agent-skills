using MackySoft.AgentDistribution.Agents.Installation.Targeting;
using MackySoft.AgentDistribution.Agents.Manifests;
using MackySoft.AgentDistribution.Digests;
using MackySoft.AgentDistribution.Shared;
using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Agents.Installation.State;

/// <summary> Inspects custom-agent ownership state and managed artifact drift without writing files. </summary>
public sealed class AgentInstalledTargetInspector
{
    private readonly AgentInstallationStatePathResolver statePathResolver;
    private readonly AgentInstallationStateStore stateStore;
    private readonly PackageContentDigestCalculator digestCalculator;

    /// <summary> Initializes a target inspector. </summary>
    public AgentInstalledTargetInspector (AgentInstallationStatePathResolver statePathResolver, AgentInstallationStateStore stateStore, PackageContentDigestCalculator digestCalculator)
    {
        this.statePathResolver = statePathResolver ?? throw new ArgumentNullException(nameof(statePathResolver));
        this.stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        this.digestCalculator = digestCalculator ?? throw new ArgumentNullException(nameof(digestCalculator));
    }

    /// <summary> Inspects one target for the specified generated agent manifest. </summary>
    public async ValueTask<AgentDistributionOperationResult<AgentInstalledTargetState>> InspectAsync (AgentManifest manifest, AgentResolvedTarget target, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(target);
        cancellationToken.ThrowIfCancellationRequested();
        var targetSafetyResult = ValidateTargetRoots(target);
        if (!targetSafetyResult.IsSuccess)
        {
            return AgentDistributionOperationResult<AgentInstalledTargetState>.FailureResult(targetSafetyResult.Failure!.Code, targetSafetyResult.Failure.Message);
        }

        var statePathResult = statePathResolver.Resolve(target, manifest.CatalogId, manifest.AgentName);
        if (!statePathResult.IsSuccess)
        {
            return AgentDistributionOperationResult<AgentInstalledTargetState>.FailureResult(statePathResult.Failure!.Code, statePathResult.Failure.Message);
        }

        var stateResult = await stateStore.ReadAsync(statePathResult.Value!, cancellationToken).ConfigureAwait(false);
        if (!stateResult.IsSuccess)
        {
            return AgentDistributionOperationResult<AgentInstalledTargetState>.Success(new AgentInstalledTargetState(AgentInstalledTargetStateKind.Invalid, stateResult.Failure!.Message));
        }

        var expectedArtifacts = manifest.HostArtifacts.Where(artifact => artifact.HostId == target.HostId).ToArray();
        if (expectedArtifacts.Length == 0)
        {
            return AgentDistributionOperationResult<AgentInstalledTargetState>.Success(new AgentInstalledTargetState(AgentInstalledTargetStateKind.Invalid, "Generated agent does not support the selected host."));
        }

        var state = stateResult.Value!.State;
        if (state is null)
        {
            var foreignStateResult = await FindForeignStateAsync(target, manifest, cancellationToken).ConfigureAwait(false);
            if (!foreignStateResult.IsSuccess)
            {
                return AgentDistributionOperationResult<AgentInstalledTargetState>.FailureResult(foreignStateResult.Failure!.Code, foreignStateResult.Failure.Message);
            }

            if (foreignStateResult.Value!.State is not null)
            {
                return AgentDistributionOperationResult<AgentInstalledTargetState>.Success(foreignStateResult.Value.State);
            }

            return await InspectUnmanagedOrMissingAsync(target, expectedArtifacts, cancellationToken).ConfigureAwait(false);
        }

        if (state.CatalogId != manifest.CatalogId)
        {
            return AgentDistributionOperationResult<AgentInstalledTargetState>.Success(new AgentInstalledTargetState(AgentInstalledTargetStateKind.OtherCatalog));
        }

        if (state.HostId != target.HostId
            || state.AgentName != manifest.AgentName)
        {
            return AgentDistributionOperationResult<AgentInstalledTargetState>.Success(new AgentInstalledTargetState(AgentInstalledTargetStateKind.Invalid, "Agent ownership state does not match the selected target."));
        }

        var managedArtifactPathResult = ValidateManagedArtifactPaths(state, expectedArtifacts);
        if (!managedArtifactPathResult.IsSuccess)
        {
            return AgentDistributionOperationResult<AgentInstalledTargetState>.Success(
                new AgentInstalledTargetState(AgentInstalledTargetStateKind.Invalid, managedArtifactPathResult.Failure!.Message));
        }

        var artifactState = await VerifyArtifactsAsync(target, state, cancellationToken).ConfigureAwait(false);
        if (!artifactState.IsSuccess)
        {
            return artifactState;
        }

        if (artifactState.Value!.Kind != AgentInstalledTargetStateKind.Current)
        {
            return artifactState;
        }

        return state.AgentManifestDigest == manifest.ManifestDigest
            ? AgentDistributionOperationResult<AgentInstalledTargetState>.Success(new AgentInstalledTargetState(AgentInstalledTargetStateKind.Current))
            : AgentDistributionOperationResult<AgentInstalledTargetState>.Success(new AgentInstalledTargetState(AgentInstalledTargetStateKind.CleanOutdated));
    }

    /// <summary> Inspects managed artifacts from ownership state when no current generated manifest is available. </summary>
    public ValueTask<AgentDistributionOperationResult<AgentInstalledTargetState>> InspectOwnedStateAsync (
        AgentInstallationState state,
        AgentResolvedTarget target,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(target);
        cancellationToken.ThrowIfCancellationRequested();
        var targetSafetyResult = ValidateTargetRoots(target);
        if (!targetSafetyResult.IsSuccess)
        {
            return ValueTask.FromResult(AgentDistributionOperationResult<AgentInstalledTargetState>.FailureResult(targetSafetyResult.Failure!.Code, targetSafetyResult.Failure.Message));
        }

        if (state.HostId != target.HostId)
        {
            return ValueTask.FromResult(AgentDistributionOperationResult<AgentInstalledTargetState>.Success(
                new AgentInstalledTargetState(AgentInstalledTargetStateKind.Invalid, "Agent ownership state host does not match the selected target.")));
        }

        return VerifyArtifactsAsync(target, state, cancellationToken);
    }

    private async ValueTask<AgentDistributionOperationResult<AgentInstalledTargetState>> InspectUnmanagedOrMissingAsync (AgentResolvedTarget target, IReadOnlyList<AgentHostArtifactManifest> expectedArtifacts, CancellationToken cancellationToken)
    {
        foreach (var artifact in expectedArtifacts)
        {
            var pathResult = AgentPathGuard.Validate(ContainedPath.Create(target.ArtifactRoot, artifact.HostTargetRelativePath.RootRelativePath));
            if (!pathResult.IsSuccess)
            {
                return AgentDistributionOperationResult<AgentInstalledTargetState>.FailureResult(pathResult.Failure!.Code, pathResult.Failure.Message);
            }

            if (File.Exists(pathResult.Value!.Value) || Directory.Exists(pathResult.Value.Value))
            {
                return AgentDistributionOperationResult<AgentInstalledTargetState>.Success(new AgentInstalledTargetState(AgentInstalledTargetStateKind.Unmanaged));
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        return AgentDistributionOperationResult<AgentInstalledTargetState>.Success(new AgentInstalledTargetState(AgentInstalledTargetStateKind.Missing));
    }

    private static AgentDistributionOperationResult<bool> ValidateTargetRoots (AgentResolvedTarget target)
    {
        var artifactResult = AgentPathGuard.Validate(ContainedPath.Create(target.ArtifactRoot, target.ArtifactRoot));
        if (!artifactResult.IsSuccess)
        {
            return AgentDistributionOperationResult<bool>.FailureResult(artifactResult.Failure!.Code, artifactResult.Failure.Message);
        }

        var stateResult = AgentPathGuard.Validate(ContainedPath.Create(target.StateRoot, target.StateRoot));
        return stateResult.IsSuccess
            ? AgentDistributionOperationResult<bool>.Success(true)
            : AgentDistributionOperationResult<bool>.FailureResult(stateResult.Failure!.Code, stateResult.Failure.Message);
    }

    private static AgentDistributionOperationResult<bool> ValidateManagedArtifactPaths (
        AgentInstallationState state,
        IReadOnlyList<AgentHostArtifactManifest> expectedArtifacts)
    {
        var expectedPaths = new List<PackageRelativePath>(expectedArtifacts.Count);
        foreach (var expectedArtifact in expectedArtifacts)
        {
            expectedPaths.Add(expectedArtifact.HostTargetRelativePath);
        }

        var managedPaths = state.ManagedArtifacts.Select(static artifact => artifact.Path).OrderBy(static path => path.Value, StringComparer.Ordinal).ToArray();
        return expectedPaths.OrderBy(static path => path.Value, StringComparer.Ordinal).SequenceEqual(managedPaths)
            ? AgentDistributionOperationResult<bool>.Success(true)
            : AgentDistributionOperationResult<bool>.FailureResult(AgentDistributionFailureCodes.ManifestInvalid, "Agent ownership state managed artifact paths do not match the generated host artifacts.");
    }

    private async ValueTask<AgentDistributionOperationResult<AgentForeignStateLookupResult>> FindForeignStateAsync (AgentResolvedTarget target, AgentManifest manifest, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(target.StateRoot.Value))
        {
            return AgentDistributionOperationResult<AgentForeignStateLookupResult>.Success(new AgentForeignStateLookupResult(null));
        }

        try
        {
            foreach (var catalogDirectory in Directory.EnumerateDirectories(target.StateRoot.Value).Order(StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var catalogPath = AbsolutePath.Parse(catalogDirectory);
                var statePath = ContainedPath.Create(catalogPath, RootRelativePath.Parse($"{manifest.AgentName.Value}.json")).Target;
                if (!ContainedPath.TryCreate(target.StateRoot, statePath, out var containedStatePath, out var containmentFailure))
                {
                    return AgentDistributionOperationResult<AgentForeignStateLookupResult>.FailureResult(
                        AgentDistributionFailureCodes.PathUnsafe,
                        $"Agent state path is invalid: {containmentFailure.Message}");
                }

                var statePathResult = AgentPathGuard.Validate(containedStatePath);
                if (!statePathResult.IsSuccess)
                {
                    return AgentDistributionOperationResult<AgentForeignStateLookupResult>.FailureResult(statePathResult.Failure!.Code, statePathResult.Failure.Message);
                }

                var stateResult = await stateStore.ReadAsync(statePathResult.Value!, cancellationToken).ConfigureAwait(false);
                if (!stateResult.IsSuccess)
                {
                    return AgentDistributionOperationResult<AgentForeignStateLookupResult>.Success(new AgentForeignStateLookupResult(new AgentInstalledTargetState(AgentInstalledTargetStateKind.Invalid, stateResult.Failure!.Message)));
                }

                var state = stateResult.Value!.State;
                if (state is not null && state.CatalogId != manifest.CatalogId)
                {
                    return AgentDistributionOperationResult<AgentForeignStateLookupResult>.Success(new AgentForeignStateLookupResult(new AgentInstalledTargetState(AgentInstalledTargetStateKind.OtherCatalog)));
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return AgentDistributionOperationResult<AgentForeignStateLookupResult>.FailureResult(AgentDistributionFailureCodes.InstallTargetReadFailed, $"Could not inspect agent installation state root: {exception.Message}");
        }

        return AgentDistributionOperationResult<AgentForeignStateLookupResult>.Success(new AgentForeignStateLookupResult(null));
    }

    private async ValueTask<AgentDistributionOperationResult<AgentInstalledTargetState>> VerifyArtifactsAsync (AgentResolvedTarget target, AgentInstallationState state, CancellationToken cancellationToken)
    {
        foreach (var artifact in state.ManagedArtifacts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pathResult = AgentPathGuard.Validate(ContainedPath.Create(target.ArtifactRoot, artifact.Path.RootRelativePath));
            if (!pathResult.IsSuccess)
            {
                return AgentDistributionOperationResult<AgentInstalledTargetState>.FailureResult(pathResult.Failure!.Code, pathResult.Failure.Message);
            }

            var path = pathResult.Value!;
            if (!File.Exists(path.Value) || Directory.Exists(path.Value))
            {
                return AgentDistributionOperationResult<AgentInstalledTargetState>.Success(new AgentInstalledTargetState(AgentInstalledTargetStateKind.LocallyModified, $"Managed agent artifact is missing: {artifact.Path}"));
            }

            try
            {
                var content = await File.ReadAllTextAsync(path.Value, cancellationToken).ConfigureAwait(false);
                if (digestCalculator.ComputeSingleFileDigest(artifact.Path, content) != artifact.Digest)
                {
                    return AgentDistributionOperationResult<AgentInstalledTargetState>.Success(new AgentInstalledTargetState(AgentInstalledTargetStateKind.LocallyModified, $"Managed agent artifact changed: {artifact.Path}"));
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return AgentDistributionOperationResult<AgentInstalledTargetState>.Success(new AgentInstalledTargetState(AgentInstalledTargetStateKind.Invalid, $"Could not read managed agent artifact: {exception.Message}"));
            }
        }

        return AgentDistributionOperationResult<AgentInstalledTargetState>.Success(new AgentInstalledTargetState(AgentInstalledTargetStateKind.Current));
    }
}
