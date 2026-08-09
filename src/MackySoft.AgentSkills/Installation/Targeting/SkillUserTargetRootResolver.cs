using MackySoft.AgentSkills.Shared;
using MackySoft.FileSystem;

namespace MackySoft.AgentSkills.Installation.Targeting;

/// <summary> Resolves host-specific user-scope SKILL roots. </summary>
public sealed class SkillUserTargetRootResolver
{
    private readonly Func<string?> homeDirectoryProvider;
    private readonly Func<string, string?> environmentVariableProvider;

    /// <summary> Initializes a new instance of the <see cref="SkillUserTargetRootResolver" /> class. </summary>
    /// <param name="homeDirectoryProvider"> Provides the current user's home directory. </param>
    /// <param name="environmentVariableProvider"> Provides process environment variables. </param>
    public SkillUserTargetRootResolver (
        Func<string?> homeDirectoryProvider,
        Func<string, string?> environmentVariableProvider)
    {
        this.homeDirectoryProvider = homeDirectoryProvider ?? throw new ArgumentNullException(nameof(homeDirectoryProvider));
        this.environmentVariableProvider = environmentVariableProvider ?? throw new ArgumentNullException(nameof(environmentVariableProvider));
    }

    /// <summary> Resolves the default user-scope SKILL root for one host. </summary>
    /// <param name="descriptor"> The host descriptor that owns the user-root policy. </param>
    /// <returns> The full host SKILL root or an environment failure. </returns>
    public SkillOperationResult<AbsolutePath> ResolveDefaultTargetRoot (SkillHostDescriptor descriptor)
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
                    return SkillOperationResult<AbsolutePath>.FailureResult(
                        SkillFailureCodes.UserTargetUnavailable,
                        $"Environment variable '{policy.EnvironmentVariableName}' must contain an absolute path for SKILL user scope.");
                }

                return policy.EnvironmentVariableChildDirectory is null
                    ? SkillOperationResult<AbsolutePath>.Success(absoluteEnvironmentRoot)
                    : ResolveUnderRoot(absoluteEnvironmentRoot, policy.EnvironmentVariableChildDirectory);
            }
        }

        return ResolveUnderHome(policy.HomeRelativeDirectory);
    }

    private SkillOperationResult<AbsolutePath> ResolveUnderHome (RootRelativePath homeRelativeDirectory)
    {
        var homeDirectory = homeDirectoryProvider();
        if (string.IsNullOrWhiteSpace(homeDirectory))
        {
            return SkillOperationResult<AbsolutePath>.FailureResult(
                SkillFailureCodes.UserTargetUnavailable,
                "Could not resolve the current user's home directory for SKILL user scope.");
        }

        if (!AbsolutePath.TryParse(homeDirectory, out var absoluteHomeDirectory, out _))
        {
            return SkillOperationResult<AbsolutePath>.FailureResult(
                SkillFailureCodes.UserTargetUnavailable,
                "Current user's home directory must be an absolute path for SKILL user scope.");
        }

        return ResolveUnderRoot(absoluteHomeDirectory, homeRelativeDirectory);
    }

    private static SkillOperationResult<AbsolutePath> ResolveUnderRoot (AbsolutePath root, RootRelativePath relativePath)
    {
        return SkillOperationResult<AbsolutePath>.Success(ContainedPath.Create(root, relativePath).Target);
    }
}
