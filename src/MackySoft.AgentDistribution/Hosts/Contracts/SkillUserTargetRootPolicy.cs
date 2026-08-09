using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Hosts.Contracts;

/// <summary> Describes how a host default user-scope SKILL root is resolved. </summary>
public sealed class SkillUserTargetRootPolicy
{
    /// <summary> Initializes one immutable user-scope host SKILL root policy. </summary>
    internal SkillUserTargetRootPolicy (
        string? environmentVariableName,
        RootRelativePath? environmentVariableChildDirectory,
        RootRelativePath homeRelativeDirectory)
    {
        if (environmentVariableName is not null && string.IsNullOrWhiteSpace(environmentVariableName))
        {
            throw new ArgumentException("Environment variable name must be null or non-whitespace.", nameof(environmentVariableName));
        }

        if (environmentVariableChildDirectory is not null)
        {
            if (environmentVariableName is null)
            {
                throw new ArgumentException("Environment variable child directory requires an environment variable name.", nameof(environmentVariableChildDirectory));
            }

        }

        ArgumentNullException.ThrowIfNull(homeRelativeDirectory);

        EnvironmentVariableName = environmentVariableName;
        EnvironmentVariableChildDirectory = environmentVariableChildDirectory;
        HomeRelativeDirectory = homeRelativeDirectory;
    }

    /// <summary> Gets the optional environment variable that overrides the home-relative host SKILL root. </summary>
    public string? EnvironmentVariableName { get; }

    /// <summary> Gets the child directory appended to the environment variable value when present. </summary>
    public RootRelativePath? EnvironmentVariableChildDirectory { get; }

    /// <summary> Gets the home-relative fallback host SKILL root directory. </summary>
    public RootRelativePath HomeRelativeDirectory { get; }
}
