using MackySoft.AgentSkills.Agents.Installation.Targeting;
using MackySoft.AgentSkills.Shared;
using MackySoft.FileSystem;

namespace MackySoft.AgentSkills.Agents.Installation.State;

/// <summary> Reads and writes canonical agent ownership-state files. </summary>
public sealed class AgentInstallationStateStore
{
    private readonly AgentInstallationStateJsonSerializer serializer;

    /// <summary> Initializes an ownership-state store. </summary>
    public AgentInstallationStateStore (AgentInstallationStateJsonSerializer serializer)
    {
        this.serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
    }

    /// <summary> Reads one ownership-state file, returning <see langword="null" /> when it is absent. </summary>
    public async ValueTask<SkillOperationResult<AgentInstallationStateReadResult>> ReadAsync (string statePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(statePath);
        cancellationToken.ThrowIfCancellationRequested();
        var pathResult = ValidateStatePath(statePath);
        if (!pathResult.IsSuccess)
        {
            return SkillOperationResult<AgentInstallationStateReadResult>.FailureResult(pathResult.Failure!.Code, pathResult.Failure.Message);
        }

        statePath = pathResult.Value!;
        if (!File.Exists(statePath))
        {
            return SkillOperationResult<AgentInstallationStateReadResult>.Success(new AgentInstallationStateReadResult());
        }

        try
        {
            var json = await File.ReadAllTextAsync(statePath, cancellationToken).ConfigureAwait(false);
            var result = serializer.TryDeserialize(json);
            return result.IsSuccess
                ? SkillOperationResult<AgentInstallationStateReadResult>.Success(new AgentInstallationStateReadResult(result.Value!))
                : SkillOperationResult<AgentInstallationStateReadResult>.FailureResult(result.Failure!.Code, result.Failure.Message);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return SkillOperationResult<AgentInstallationStateReadResult>.FailureResult(SkillFailureCodes.InstallTargetReadFailed, $"Could not read agent installation state: {exception.Message}");
        }
    }

    /// <summary> Writes one canonical ownership-state file. </summary>
    public async ValueTask<SkillOperationResult<bool>> WriteAsync (string statePath, AgentInstallationState state, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(statePath);
        ArgumentNullException.ThrowIfNull(state);
        cancellationToken.ThrowIfCancellationRequested();
        var pathResult = ValidateStatePath(statePath);
        if (!pathResult.IsSuccess)
        {
            return SkillOperationResult<bool>.FailureResult(pathResult.Failure!.Code, pathResult.Failure.Message);
        }

        statePath = pathResult.Value!;
        var temporaryPath = statePath + $".tmp.{Guid.NewGuid():N}";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(statePath)!);
            await File.WriteAllTextAsync(temporaryPath, serializer.Serialize(state), cancellationToken).ConfigureAwait(false);
            ReplaceFile(temporaryPath, statePath);
            return SkillOperationResult<bool>.Success(true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return SkillOperationResult<bool>.FailureResult(SkillFailureCodes.InstallTargetWriteFailed, $"Could not write agent installation state: {exception.Message}");
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // NOTE: The write result remains authoritative after a replacement; cleanup can be retried separately.
            }
        }
    }

    private static void ReplaceFile (string temporaryPath, string statePath)
    {
        try
        {
            File.Replace(temporaryPath, statePath, destinationBackupFileName: null, ignoreMetadataErrors: true);
        }
        catch (FileNotFoundException)
        {
            File.Move(temporaryPath, statePath, overwrite: true);
        }
    }

    private static SkillOperationResult<string> ValidateStatePath (string statePath)
    {
        if (!AbsolutePath.TryParse(statePath, out var absoluteStatePath, out var failure))
        {
            return SkillOperationResult<string>.FailureResult(
                SkillFailureCodes.PathUnsafe,
                $"Agent installation state path must be absolute: {failure.Message}");
        }

        return absoluteStatePath.TryGetParent(out var directory)
            ? AgentPathGuard.ResolveUnderRoot(directory.Value, absoluteStatePath.Value)
            : SkillOperationResult<string>.FailureResult(SkillFailureCodes.PathUnsafe, "Agent installation state path must have a directory.");
    }
}
