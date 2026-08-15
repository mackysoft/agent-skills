using MackySoft.AgentDistribution.Agents.Installation.State;
using MackySoft.AgentDistribution.Agents.Installation.Targeting;
using MackySoft.AgentDistribution.Serialization;
using MackySoft.AgentDistribution.Shared;
using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Agents.Installation.Services;

internal sealed class AgentManagedArtifactStore : IAgentManagedArtifactStore
{
    private readonly AgentInstallationStatePathResolver statePathResolver;
    private readonly AgentInstallationStateStore stateStore;

    public AgentManagedArtifactStore (AgentInstallationStatePathResolver statePathResolver, AgentInstallationStateStore stateStore)
    {
        this.statePathResolver = statePathResolver ?? throw new ArgumentNullException(nameof(statePathResolver));
        this.stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
    }

    public async ValueTask<AgentDistributionOperationResult<bool>> WriteAsync (
        AgentReconciliationPlan plan,
        AgentResolvedTarget target,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var statePathResult = statePathResolver.Resolve(target, plan.Package.Manifest.CatalogId, plan.Package.Manifest.AgentName);
        if (!statePathResult.IsSuccess)
        {
            return AgentDistributionOperationResult<bool>.FailureResult(statePathResult.Failure!.Code, statePathResult.Failure.Message);
        }

        // NOTE: A new install records ownership first so an interrupted first write remains recoverable as managed drift.
        if (plan.TargetState.Kind == AgentInstalledTargetStateKind.Missing)
        {
            var stateWriteResult = await stateStore.WriteAsync(statePathResult.Value!, plan.DesiredState, cancellationToken).ConfigureAwait(false);
            if (!stateWriteResult.IsSuccess)
            {
                return stateWriteResult;
            }
        }

        foreach (var artifact in plan.Artifacts)
        {
            var writeResult = await WriteArtifactAsync(target, artifact, cancellationToken).ConfigureAwait(false);
            if (!writeResult.IsSuccess)
            {
                return writeResult;
            }
        }

        if (plan.TargetState.Kind != AgentInstalledTargetStateKind.Missing)
        {
            return await stateStore.WriteAsync(statePathResult.Value!, plan.DesiredState, cancellationToken).ConfigureAwait(false);
        }

        return AgentDistributionOperationResult<bool>.Success(true);
    }

    public async ValueTask<AgentDistributionOperationResult<bool>> DeleteAsync (
        AgentInstallationState state,
        AbsolutePath statePath,
        AgentResolvedTarget target,
        Func<CancellationToken, ValueTask<AgentDistributionOperationResult<bool>>>? precondition,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (precondition is not null)
        {
            var preconditionResult = await precondition(cancellationToken).ConfigureAwait(false);
            if (!preconditionResult.IsSuccess)
            {
                return preconditionResult;
            }
        }

        try
        {
            foreach (var artifact in state.ManagedArtifacts)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var pathResult = AgentPathGuard.Validate(ContainedPath.Create(target.ArtifactRoot, artifact.Path.RootRelativePath));
                if (!pathResult.IsSuccess)
                {
                    return AgentDistributionOperationResult<bool>.FailureResult(pathResult.Failure!.Code, pathResult.Failure.Message);
                }

                if (Directory.Exists(pathResult.Value!.Value))
                {
                    return AgentDistributionOperationResult<bool>.FailureResult(
                        AgentDistributionFailureCodes.InstallTargetUnmanaged,
                        $"Managed agent artifact path is occupied by a directory: {artifact.Path}.");
                }

                File.Delete(pathResult.Value!.Value);
            }

            File.Delete(statePath.Value);
            return AgentDistributionOperationResult<bool>.Success(true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return AgentDistributionOperationResult<bool>.FailureResult(
                AgentDistributionFailureCodes.InstallTargetWriteFailed,
                $"Could not delete managed agent installation: {exception.Message}");
        }
    }

    private static async ValueTask<AgentDistributionOperationResult<bool>> WriteArtifactAsync (
        AgentResolvedTarget target,
        AgentPlannedArtifact artifact,
        CancellationToken cancellationToken)
    {
        var pathResult = AgentPathGuard.Validate(ContainedPath.Create(target.ArtifactRoot, artifact.RelativePath.RootRelativePath));
        if (!pathResult.IsSuccess)
        {
            return AgentDistributionOperationResult<bool>.FailureResult(pathResult.Failure!.Code, pathResult.Failure.Message);
        }

        var targetPath = pathResult.Value!;
        try
        {
            await CanonicalTextFilePublisher.PublishAsync(targetPath, artifact.Content, cancellationToken).ConfigureAwait(false);
            return AgentDistributionOperationResult<bool>.Success(true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return AgentDistributionOperationResult<bool>.FailureResult(
                AgentDistributionFailureCodes.InstallTargetWriteFailed,
                $"Could not write managed agent artifact '{artifact.RelativePath}': {exception.Message}");
        }
    }
}
