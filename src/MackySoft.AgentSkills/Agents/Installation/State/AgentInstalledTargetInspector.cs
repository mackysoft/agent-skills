using MackySoft.AgentSkills.Agents.Installation.Targeting;
using MackySoft.AgentSkills.Agents.Manifests;
using MackySoft.AgentSkills.Digests;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Agents.Installation.State;

/// <summary> Inspects custom-agent ownership state and managed artifact drift without writing files. </summary>
public sealed class AgentInstalledTargetInspector
{
    private readonly AgentInstallationStatePathResolver statePathResolver;
    private readonly AgentInstallationStateStore stateStore;
    private readonly SkillDigestCalculator digestCalculator;

    /// <summary> Initializes a target inspector. </summary>
    public AgentInstalledTargetInspector (AgentInstallationStatePathResolver statePathResolver, AgentInstallationStateStore stateStore, SkillDigestCalculator digestCalculator)
    {
        this.statePathResolver = statePathResolver ?? throw new ArgumentNullException(nameof(statePathResolver));
        this.stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        this.digestCalculator = digestCalculator ?? throw new ArgumentNullException(nameof(digestCalculator));
    }

    /// <summary> Inspects one target for the specified generated agent manifest. </summary>
    public async ValueTask<SkillOperationResult<AgentInstalledTargetState>> InspectAsync (AgentManifest manifest, AgentResolvedTarget target, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(target);
        cancellationToken.ThrowIfCancellationRequested();
        var targetSafetyResult = ValidateTargetRoots(target);
        if (!targetSafetyResult.IsSuccess)
        {
            return SkillOperationResult<AgentInstalledTargetState>.FailureResult(targetSafetyResult.Failure!.Code, targetSafetyResult.Failure.Message);
        }

        var statePathResult = statePathResolver.Resolve(target, manifest.CatalogId, manifest.AgentName);
        if (!statePathResult.IsSuccess)
        {
            return SkillOperationResult<AgentInstalledTargetState>.FailureResult(statePathResult.Failure!.Code, statePathResult.Failure.Message);
        }

        var stateResult = await stateStore.ReadAsync(statePathResult.Value!, cancellationToken).ConfigureAwait(false);
        if (!stateResult.IsSuccess)
        {
            return SkillOperationResult<AgentInstalledTargetState>.Success(new AgentInstalledTargetState(AgentInstalledTargetStateKind.Invalid, stateResult.Failure!.Message));
        }

        var expectedArtifacts = manifest.HostArtifacts.Where(artifact => artifact.HostId == target.HostId).ToArray();
        if (expectedArtifacts.Length == 0)
        {
            return SkillOperationResult<AgentInstalledTargetState>.Success(new AgentInstalledTargetState(AgentInstalledTargetStateKind.Invalid, "Generated agent does not support the selected host."));
        }

        var state = stateResult.Value!.State;
        if (state is null)
        {
            var foreignStateResult = await FindForeignStateAsync(target, manifest, cancellationToken).ConfigureAwait(false);
            if (!foreignStateResult.IsSuccess)
            {
                return SkillOperationResult<AgentInstalledTargetState>.FailureResult(foreignStateResult.Failure!.Code, foreignStateResult.Failure.Message);
            }

            if (foreignStateResult.Value!.State is not null)
            {
                return SkillOperationResult<AgentInstalledTargetState>.Success(foreignStateResult.Value.State);
            }

            return await InspectUnmanagedOrMissingAsync(target, expectedArtifacts, cancellationToken).ConfigureAwait(false);
        }

        if (state.CatalogId != manifest.CatalogId)
        {
            return SkillOperationResult<AgentInstalledTargetState>.Success(new AgentInstalledTargetState(AgentInstalledTargetStateKind.OtherCatalog));
        }

        if (state.HostId != target.HostId
            || state.Category != manifest.Category
            || state.AgentName != manifest.AgentName)
        {
            return SkillOperationResult<AgentInstalledTargetState>.Success(new AgentInstalledTargetState(AgentInstalledTargetStateKind.Invalid, "Agent ownership state does not match the selected target."));
        }

        var managedArtifactPathResult = ValidateManagedArtifactPaths(state, expectedArtifacts, target.HostId);
        if (!managedArtifactPathResult.IsSuccess)
        {
            return SkillOperationResult<AgentInstalledTargetState>.Success(
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
            ? SkillOperationResult<AgentInstalledTargetState>.Success(new AgentInstalledTargetState(AgentInstalledTargetStateKind.Current))
            : SkillOperationResult<AgentInstalledTargetState>.Success(new AgentInstalledTargetState(AgentInstalledTargetStateKind.CleanOutdated));
    }

    /// <summary> Inspects managed artifacts from ownership state when no current generated manifest is available. </summary>
    public ValueTask<SkillOperationResult<AgentInstalledTargetState>> InspectOwnedStateAsync (
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
            return ValueTask.FromResult(SkillOperationResult<AgentInstalledTargetState>.FailureResult(targetSafetyResult.Failure!.Code, targetSafetyResult.Failure.Message));
        }

        if (state.HostId != target.HostId)
        {
            return ValueTask.FromResult(SkillOperationResult<AgentInstalledTargetState>.Success(
                new AgentInstalledTargetState(AgentInstalledTargetStateKind.Invalid, "Agent ownership state host does not match the selected target.")));
        }

        return VerifyArtifactsAsync(target, state, cancellationToken);
    }

    private async ValueTask<SkillOperationResult<AgentInstalledTargetState>> InspectUnmanagedOrMissingAsync (AgentResolvedTarget target, IReadOnlyList<AgentHostArtifactManifest> expectedArtifacts, CancellationToken cancellationToken)
    {
        foreach (var artifact in expectedArtifacts)
        {
            var relativePathResult = AgentHostArtifactPath.ResolveTargetRelativePath(artifact.Path, target.HostId);
            if (!relativePathResult.IsSuccess)
            {
                return SkillOperationResult<AgentInstalledTargetState>.Success(new AgentInstalledTargetState(AgentInstalledTargetStateKind.Invalid, relativePathResult.Failure!.Message));
            }

            var pathResult = AgentPathGuard.ResolveArtifactPath(target.ArtifactRoot, relativePathResult.Value!);
            if (!pathResult.IsSuccess)
            {
                return SkillOperationResult<AgentInstalledTargetState>.FailureResult(pathResult.Failure!.Code, pathResult.Failure.Message);
            }

            if (File.Exists(pathResult.Value!) || Directory.Exists(pathResult.Value!))
            {
                return SkillOperationResult<AgentInstalledTargetState>.Success(new AgentInstalledTargetState(AgentInstalledTargetStateKind.Unmanaged));
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        return SkillOperationResult<AgentInstalledTargetState>.Success(new AgentInstalledTargetState(AgentInstalledTargetStateKind.Missing));
    }

    private static SkillOperationResult<bool> ValidateTargetRoots (AgentResolvedTarget target)
    {
        var artifactResult = AgentPathGuard.ResolveStandaloneRoot(target.ArtifactRoot);
        if (!artifactResult.IsSuccess)
        {
            return SkillOperationResult<bool>.FailureResult(artifactResult.Failure!.Code, artifactResult.Failure.Message);
        }

        var stateResult = AgentPathGuard.ResolveStandaloneRoot(target.StateRoot);
        return stateResult.IsSuccess
            ? SkillOperationResult<bool>.Success(true)
            : SkillOperationResult<bool>.FailureResult(stateResult.Failure!.Code, stateResult.Failure.Message);
    }

    private static SkillOperationResult<bool> ValidateManagedArtifactPaths (
        AgentInstallationState state,
        IReadOnlyList<AgentHostArtifactManifest> expectedArtifacts,
        AgentHostKind hostId)
    {
        var expectedPaths = new List<string>(expectedArtifacts.Count);
        foreach (var expectedArtifact in expectedArtifacts)
        {
            var pathResult = AgentHostArtifactPath.ResolveTargetRelativePath(expectedArtifact.Path, hostId);
            if (!pathResult.IsSuccess)
            {
                return SkillOperationResult<bool>.FailureResult(pathResult.Failure!.Code, pathResult.Failure.Message);
            }

            expectedPaths.Add(pathResult.Value!);
        }

        var managedPaths = state.ManagedArtifacts.Select(static artifact => artifact.Path).Order(StringComparer.Ordinal).ToArray();
        return expectedPaths.Order(StringComparer.Ordinal).SequenceEqual(managedPaths, StringComparer.Ordinal)
            ? SkillOperationResult<bool>.Success(true)
            : SkillOperationResult<bool>.FailureResult(SkillFailureCodes.ManifestInvalid, "Agent ownership state managed artifact paths do not match the generated host artifacts.");
    }

    private async ValueTask<SkillOperationResult<AgentForeignStateLookupResult>> FindForeignStateAsync (AgentResolvedTarget target, AgentManifest manifest, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(target.StateRoot))
        {
            return SkillOperationResult<AgentForeignStateLookupResult>.Success(new AgentForeignStateLookupResult(null));
        }

        try
        {
            foreach (var catalogDirectory in Directory.EnumerateDirectories(target.StateRoot).Order(StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var statePathResult = AgentPathGuard.ResolveUnderRoot(catalogDirectory, Path.Combine(catalogDirectory, $"{manifest.AgentName.Value}.json"));
                if (!statePathResult.IsSuccess)
                {
                    return SkillOperationResult<AgentForeignStateLookupResult>.FailureResult(statePathResult.Failure!.Code, statePathResult.Failure.Message);
                }

                var stateResult = await stateStore.ReadAsync(statePathResult.Value!, cancellationToken).ConfigureAwait(false);
                if (!stateResult.IsSuccess)
                {
                    return SkillOperationResult<AgentForeignStateLookupResult>.Success(new AgentForeignStateLookupResult(new AgentInstalledTargetState(AgentInstalledTargetStateKind.Invalid, stateResult.Failure!.Message)));
                }

                var state = stateResult.Value!.State;
                if (state is not null && state.CatalogId != manifest.CatalogId)
                {
                    return SkillOperationResult<AgentForeignStateLookupResult>.Success(new AgentForeignStateLookupResult(new AgentInstalledTargetState(AgentInstalledTargetStateKind.OtherCatalog)));
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return SkillOperationResult<AgentForeignStateLookupResult>.FailureResult(SkillFailureCodes.InstallTargetReadFailed, $"Could not inspect agent installation state root: {exception.Message}");
        }

        return SkillOperationResult<AgentForeignStateLookupResult>.Success(new AgentForeignStateLookupResult(null));
    }

    private async ValueTask<SkillOperationResult<AgentInstalledTargetState>> VerifyArtifactsAsync (AgentResolvedTarget target, AgentInstallationState state, CancellationToken cancellationToken)
    {
        foreach (var artifact in state.ManagedArtifacts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pathResult = AgentPathGuard.ResolveArtifactPath(target.ArtifactRoot, artifact.Path);
            if (!pathResult.IsSuccess)
            {
                return SkillOperationResult<AgentInstalledTargetState>.FailureResult(pathResult.Failure!.Code, pathResult.Failure.Message);
            }

            var path = pathResult.Value!;
            if (!File.Exists(path) || Directory.Exists(path))
            {
                return SkillOperationResult<AgentInstalledTargetState>.Success(new AgentInstalledTargetState(AgentInstalledTargetStateKind.LocallyModified, $"Managed agent artifact is missing: {artifact.Path}"));
            }

            try
            {
                var content = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
                if (digestCalculator.ComputeSingleFileDigest(artifact.Path, content) != artifact.Digest)
                {
                    return SkillOperationResult<AgentInstalledTargetState>.Success(new AgentInstalledTargetState(AgentInstalledTargetStateKind.LocallyModified, $"Managed agent artifact changed: {artifact.Path}"));
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return SkillOperationResult<AgentInstalledTargetState>.Success(new AgentInstalledTargetState(AgentInstalledTargetStateKind.Invalid, $"Could not read managed agent artifact: {exception.Message}"));
            }
        }

        return SkillOperationResult<AgentInstalledTargetState>.Success(new AgentInstalledTargetState(AgentInstalledTargetStateKind.Current));
    }
}
