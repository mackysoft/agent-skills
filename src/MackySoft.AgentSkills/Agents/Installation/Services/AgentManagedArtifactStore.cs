using MackySoft.AgentSkills.Agents.Installation.State;
using MackySoft.AgentSkills.Agents.Installation.Targeting;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Agents.Installation.Services;

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
        string statePath,
        AgentResolvedTarget target,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            foreach (var artifact in state.ManagedArtifacts)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var pathResult = AgentPathGuard.ResolveArtifactPath(target.ArtifactRoot, artifact.Path);
                if (!pathResult.IsSuccess)
                {
                    return SkillOperationResult<bool>.FailureResult(pathResult.Failure!.Code, pathResult.Failure.Message);
                }

                if (Directory.Exists(pathResult.Value!))
                {
                    return SkillOperationResult<bool>.FailureResult(
                        SkillFailureCodes.InstallTargetUnmanaged,
                        $"Managed agent artifact path is occupied by a directory: {artifact.Path}.");
                }

                File.Delete(pathResult.Value!);
            }

            File.Delete(statePath);
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
        var pathResult = AgentPathGuard.ResolveArtifactPath(target.ArtifactRoot, artifact.RelativePath);
        if (!pathResult.IsSuccess)
        {
            return SkillOperationResult<bool>.FailureResult(pathResult.Failure!.Code, pathResult.Failure.Message);
        }

        var targetPath = pathResult.Value!;
        var temporaryPath = targetPath + $".tmp.{Guid.NewGuid():N}";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            await File.WriteAllTextAsync(temporaryPath, artifact.Content, cancellationToken).ConfigureAwait(false);
            ReplaceFile(temporaryPath, targetPath);
            return SkillOperationResult<bool>.Success(true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return SkillOperationResult<bool>.FailureResult(
                SkillFailureCodes.InstallTargetWriteFailed,
                $"Could not write managed agent artifact '{artifact.RelativePath}': {exception.Message}");
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static void ReplaceFile (string temporaryPath, string targetPath)
    {
        try
        {
            File.Replace(temporaryPath, targetPath, destinationBackupFileName: null, ignoreMetadataErrors: true);
        }
        catch (FileNotFoundException)
        {
            File.Move(temporaryPath, targetPath, overwrite: true);
        }
    }
}
