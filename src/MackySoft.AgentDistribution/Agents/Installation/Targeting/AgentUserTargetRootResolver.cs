using MackySoft.AgentDistribution.Shared;
using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Agents.Installation.Targeting;

/// <summary> Resolves host-owned user-scope agent artifact and state roots. </summary>
public sealed class AgentUserTargetRootResolver
{
    private readonly Func<string?> homeDirectoryProvider;
    private readonly Func<string, string?> environmentVariableProvider;

    /// <summary> Initializes a user-scope target-root resolver. </summary>
    public AgentUserTargetRootResolver (Func<string?> homeDirectoryProvider, Func<string, string?> environmentVariableProvider)
    {
        this.homeDirectoryProvider = homeDirectoryProvider ?? throw new ArgumentNullException(nameof(homeDirectoryProvider));
        this.environmentVariableProvider = environmentVariableProvider ?? throw new ArgumentNullException(nameof(environmentVariableProvider));
    }

    /// <summary> Resolves the default user-scope roots for one agent host. </summary>
    public SkillOperationResult<AgentUserTargetRoots> ResolveDefaultTargetRoots (AgentHostTargetPolicy descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        var policy = descriptor.UserTargetRootPolicy;
        if (policy.EnvironmentVariableName is not null)
        {
            var environmentRoot = environmentVariableProvider(policy.EnvironmentVariableName);
            if (!string.IsNullOrWhiteSpace(environmentRoot))
            {
                if (!AbsolutePath.TryParse(environmentRoot, out var absoluteEnvironmentRoot, out _))
                {
                    return Failure($"Environment variable '{policy.EnvironmentVariableName}' must contain an absolute path for agent user scope.");
                }

                return CreateRoots(
                    ContainedPath.Create(absoluteEnvironmentRoot, policy.EnvironmentArtifactChildDirectory!),
                    ContainedPath.Create(absoluteEnvironmentRoot, policy.EnvironmentStateChildDirectory!));
            }
        }

        var home = homeDirectoryProvider();
        if (string.IsNullOrWhiteSpace(home)
            || !AbsolutePath.TryParse(home, out var absoluteHome, out _))
        {
            return Failure("Could not resolve an absolute user home directory for agent user scope.");
        }

        return CreateRoots(
            ContainedPath.Create(absoluteHome, policy.HomeArtifactRelativeDirectory),
            ContainedPath.Create(absoluteHome, policy.HomeStateRelativeDirectory));
    }

    private static SkillOperationResult<AgentUserTargetRoots> CreateRoots (ContainedPath artifactRoot, ContainedPath stateRoot)
    {
        var artifactResult = AgentPathGuard.Validate(artifactRoot);
        if (!artifactResult.IsSuccess)
        {
            return SkillOperationResult<AgentUserTargetRoots>.FailureResult(artifactResult.Failure!.Code, artifactResult.Failure.Message);
        }

        var stateResult = AgentPathGuard.Validate(stateRoot);
        return stateResult.IsSuccess
            ? SkillOperationResult<AgentUserTargetRoots>.Success(new AgentUserTargetRoots(
                artifactResult.Value!,
                stateResult.Value!))
            : SkillOperationResult<AgentUserTargetRoots>.FailureResult(stateResult.Failure!.Code, stateResult.Failure.Message);
    }

    private static SkillOperationResult<AgentUserTargetRoots> Failure (string message)
    {
        return SkillOperationResult<AgentUserTargetRoots>.FailureResult(SkillFailureCodes.UserTargetUnavailable, message);
    }
}
