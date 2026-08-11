using MackySoft.AgentDistribution.Agents.Installation.Targeting;
using MackySoft.AgentDistribution.Serialization;
using MackySoft.AgentDistribution.Shared;
using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Agents.Installation.State;

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
    public async ValueTask<AgentDistributionOperationResult<AgentInstallationStateReadResult>> ReadAsync (AbsolutePath statePath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(statePath);
        cancellationToken.ThrowIfCancellationRequested();
        var pathResult = ValidateStatePath(statePath);
        if (!pathResult.IsSuccess)
        {
            return AgentDistributionOperationResult<AgentInstallationStateReadResult>.FailureResult(pathResult.Failure!.Code, pathResult.Failure.Message);
        }

        var validatedPath = pathResult.Value!;
        if (!File.Exists(validatedPath.Value))
        {
            return AgentDistributionOperationResult<AgentInstallationStateReadResult>.Success(new AgentInstallationStateReadResult());
        }

        try
        {
            var json = await File.ReadAllTextAsync(validatedPath.Value, cancellationToken).ConfigureAwait(false);
            var result = serializer.TryDeserialize(json);
            return result.IsSuccess
                ? AgentDistributionOperationResult<AgentInstallationStateReadResult>.Success(new AgentInstallationStateReadResult(result.Value!))
                : AgentDistributionOperationResult<AgentInstallationStateReadResult>.FailureResult(result.Failure!.Code, result.Failure.Message);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return AgentDistributionOperationResult<AgentInstallationStateReadResult>.FailureResult(AgentDistributionFailureCodes.InstallTargetReadFailed, $"Could not read agent installation state: {exception.Message}");
        }
    }

    /// <summary> Writes one canonical ownership-state file. </summary>
    public async ValueTask<AgentDistributionOperationResult<bool>> WriteAsync (AbsolutePath statePath, AgentInstallationState state, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(statePath);
        ArgumentNullException.ThrowIfNull(state);
        cancellationToken.ThrowIfCancellationRequested();
        var pathResult = ValidateStatePath(statePath);
        if (!pathResult.IsSuccess)
        {
            return AgentDistributionOperationResult<bool>.FailureResult(pathResult.Failure!.Code, pathResult.Failure.Message);
        }

        var validatedPath = pathResult.Value!;
        try
        {
            await CanonicalTextFilePublisher.PublishAsync(
                validatedPath,
                serializer.Serialize(state),
                cancellationToken).ConfigureAwait(false);
            return AgentDistributionOperationResult<bool>.Success(true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return AgentDistributionOperationResult<bool>.FailureResult(AgentDistributionFailureCodes.InstallTargetWriteFailed, $"Could not write agent installation state: {exception.Message}");
        }
    }

    private static AgentDistributionOperationResult<AbsolutePath> ValidateStatePath (AbsolutePath statePath)
    {
        return statePath.TryGetParent(out var directory)
            ? AgentPathGuard.Validate(ContainedPath.Create(directory, statePath))
            : AgentDistributionOperationResult<AbsolutePath>.FailureResult(AgentDistributionFailureCodes.PathUnsafe, "Agent installation state path must have a directory.");
    }
}
