using MackySoft.AgentDistribution.Agents.Installation.State;
using MackySoft.AgentDistribution.Agents.Installation.Targeting;
using MackySoft.AgentDistribution.Serialization;
using MackySoft.AgentDistribution.Shared;
using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Agents.Installation.Services;

internal sealed class AgentManagedArtifactStore
{
    private readonly AgentInstallationStatePathResolver statePathResolver;
    private readonly AgentInstallationStateStore stateStore;

    public AgentManagedArtifactStore (AgentInstallationStatePathResolver statePathResolver, AgentInstallationStateStore stateStore)
    {
        this.statePathResolver = statePathResolver ?? throw new ArgumentNullException(nameof(statePathResolver));
        this.stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
    }

    public async ValueTask<SkillOperationResult<bool>> WriteAsync (
        AgentReconciliationPlan plan,
        AgentResolvedTarget target,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var statePathResult = statePathResolver.Resolve(target, plan.Package.Manifest.CatalogId, plan.Package.Manifest.AgentName);
        if (!statePathResult.IsSuccess)
        {
            return SkillOperationResult<bool>.FailureResult(statePathResult.Failure!.Code, statePathResult.Failure.Message);
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

        return SkillOperationResult<bool>.Success(true);
    }

    public async ValueTask<SkillOperationResult<bool>> DeleteAsync (
        AgentInstallationState state,
        AbsolutePath statePath,
        AgentResolvedTarget target,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            foreach (var artifact in state.ManagedArtifacts)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var pathResult = AgentPathGuard.Validate(ContainedPath.Create(target.ArtifactRoot, artifact.Path.RootRelativePath));
                if (!pathResult.IsSuccess)
                {
                    return SkillOperationResult<bool>.FailureResult(pathResult.Failure!.Code, pathResult.Failure.Message);
                }

                if (Directory.Exists(pathResult.Value!.Value))
                {
                    return SkillOperationResult<bool>.FailureResult(
                        SkillFailureCodes.InstallTargetUnmanaged,
                        $"Managed agent artifact path is occupied by a directory: {artifact.Path}.");
                }

                File.Delete(pathResult.Value!.Value);
            }

            File.Delete(statePath.Value);
            return SkillOperationResult<bool>.Success(true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return SkillOperationResult<bool>.FailureResult(
                SkillFailureCodes.InstallTargetWriteFailed,
                $"Could not delete managed agent installation: {exception.Message}");
        }
    }

    private static async ValueTask<SkillOperationResult<bool>> WriteArtifactAsync (
        AgentResolvedTarget target,
        AgentPlannedArtifact artifact,
        CancellationToken cancellationToken)
    {
        var pathResult = AgentPathGuard.Validate(ContainedPath.Create(target.ArtifactRoot, artifact.RelativePath.RootRelativePath));
        if (!pathResult.IsSuccess)
        {
            return SkillOperationResult<bool>.FailureResult(pathResult.Failure!.Code, pathResult.Failure.Message);
        }

        var targetPath = pathResult.Value!;
        try
        {
            await CanonicalTextFilePublisher.PublishAsync(targetPath, artifact.Content, cancellationToken).ConfigureAwait(false);
            return SkillOperationResult<bool>.Success(true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return SkillOperationResult<bool>.FailureResult(
                SkillFailureCodes.InstallTargetWriteFailed,
                $"Could not write managed agent artifact '{artifact.RelativePath}': {exception.Message}");
        }
    }
}
