using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Agents.Hosts;

/// <summary> Describes one host's user-scope artifact and installation-state roots. </summary>
public sealed class AgentUserTargetRootPolicy
{
    /// <summary> Initializes an immutable user-scope target policy. </summary>
    internal AgentUserTargetRootPolicy (
        string? environmentVariableName,
        string? environmentArtifactChildDirectory,
        string? environmentStateChildDirectory,
        string homeArtifactRelativeDirectory,
        string homeStateRelativeDirectory)
    {
        if (environmentVariableName is not null && string.IsNullOrWhiteSpace(environmentVariableName))
        {
            throw new ArgumentException("Environment variable name must be null or non-whitespace.", nameof(environmentVariableName));
        }

        if ((environmentArtifactChildDirectory is null) != (environmentStateChildDirectory is null))
        {
            throw new ArgumentException("Environment artifact and state child directories must be specified together.");
        }

        if (environmentArtifactChildDirectory is not null
            && (!PackageRelativePath.TryParse(environmentArtifactChildDirectory, out _)
                || !PackageRelativePath.TryParse(environmentStateChildDirectory, out _)))
        {
            throw new ArgumentException("Environment child directories must be safe relative paths.");
        }

        if (!PackageRelativePath.TryParse(homeArtifactRelativeDirectory, out _)
            || !PackageRelativePath.TryParse(homeStateRelativeDirectory, out _))
        {
            throw new ArgumentException("Home-relative target directories must be safe relative paths.");
        }

        EnvironmentVariableName = environmentVariableName;
        EnvironmentArtifactChildDirectory = environmentArtifactChildDirectory;
        EnvironmentStateChildDirectory = environmentStateChildDirectory;
        HomeArtifactRelativeDirectory = homeArtifactRelativeDirectory;
        HomeStateRelativeDirectory = homeStateRelativeDirectory;
    }

    /// <summary> Gets the optional host-home override environment variable. </summary>
    public string? EnvironmentVariableName { get; }

    /// <summary> Gets the artifact directory appended to the environment-variable value. </summary>
    public string? EnvironmentArtifactChildDirectory { get; }

    /// <summary> Gets the state directory appended to the environment-variable value. </summary>
    public string? EnvironmentStateChildDirectory { get; }

    /// <summary> Gets the artifact directory relative to the user's home. </summary>
    public string HomeArtifactRelativeDirectory { get; }

    /// <summary> Gets the installation-state directory relative to the user's home. </summary>
    public string HomeStateRelativeDirectory { get; }
}
