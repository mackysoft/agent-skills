using MackySoft.AgentSkills.Agents.Installation.Targeting;
using MackySoft.AgentSkills.Serialization;
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
    public async ValueTask<SkillOperationResult<AgentInstallationStateReadResult>> ReadAsync (AbsolutePath statePath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(statePath);
        cancellationToken.ThrowIfCancellationRequested();
        var pathResult = ValidateStatePath(statePath);
        if (!pathResult.IsSuccess)
        {
            return SkillOperationResult<AgentInstallationStateReadResult>.FailureResult(pathResult.Failure!.Code, pathResult.Failure.Message);
        }

        var validatedPath = pathResult.Value!;
        if (!File.Exists(validatedPath.Value))
        {
            return SkillOperationResult<AgentInstallationStateReadResult>.Success(new AgentInstallationStateReadResult());
        }

        try
        {
            var json = await File.ReadAllTextAsync(validatedPath.Value, cancellationToken).ConfigureAwait(false);
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
    public async ValueTask<SkillOperationResult<bool>> WriteAsync (AbsolutePath statePath, AgentInstallationState state, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(statePath);
        ArgumentNullException.ThrowIfNull(state);
        cancellationToken.ThrowIfCancellationRequested();
        var pathResult = ValidateStatePath(statePath);
        if (!pathResult.IsSuccess)
        {
            return SkillOperationResult<bool>.FailureResult(pathResult.Failure!.Code, pathResult.Failure.Message);
        }

        var validatedPath = pathResult.Value!;
        try
        {
            await CanonicalTextFilePublisher.PublishAsync(
                validatedPath,
                serializer.Serialize(state),
                cancellationToken).ConfigureAwait(false);
            return SkillOperationResult<bool>.Success(true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return SkillOperationResult<bool>.FailureResult(SkillFailureCodes.InstallTargetWriteFailed, $"Could not write agent installation state: {exception.Message}");
        }
    }

    private static SkillOperationResult<AbsolutePath> ValidateStatePath (AbsolutePath statePath)
    {
        return statePath.TryGetParent(out var directory)
            ? AgentPathGuard.Validate(ContainedPath.Create(directory, statePath))
            : SkillOperationResult<AbsolutePath>.FailureResult(SkillFailureCodes.PathUnsafe, "Agent installation state path must have a directory.");
    }
}
