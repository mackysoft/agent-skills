using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Hosts.Contracts;

/// <summary> Describes how a host default user-scope target root is resolved. </summary>
public sealed class SkillUserTargetRootPolicy
{
    /// <summary> Initializes one immutable user target root policy. </summary>
    internal SkillUserTargetRootPolicy (
        string? environmentVariableName,
        string? environmentVariableChildDirectory,
        string homeRelativeDirectory)
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

            if (!SkillRelativePath.IsSafeFilePath(environmentVariableChildDirectory))
            {
                throw new ArgumentException("Environment variable child directory must be a safe relative path.", nameof(environmentVariableChildDirectory));
            }
        }

        if (!SkillRelativePath.IsSafeFilePath(homeRelativeDirectory))
        {
            throw new ArgumentException("Home-relative target directory must be a safe relative path.", nameof(homeRelativeDirectory));
        }

        EnvironmentVariableName = environmentVariableName;
        EnvironmentVariableChildDirectory = environmentVariableChildDirectory;
        HomeRelativeDirectory = homeRelativeDirectory;
    }

    /// <summary> Gets the optional environment variable that overrides the home-relative target root. </summary>
    public string? EnvironmentVariableName { get; }

    /// <summary> Gets the child directory appended to the environment variable value when present. </summary>
    public string? EnvironmentVariableChildDirectory { get; }

    /// <summary> Gets the home-relative fallback target directory. </summary>
    public string HomeRelativeDirectory { get; }
}
