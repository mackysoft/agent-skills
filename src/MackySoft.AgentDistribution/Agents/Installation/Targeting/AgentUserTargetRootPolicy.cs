using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Agents.Installation.Targeting;

/// <summary> Describes one host's user-scope artifact and installation-state roots. </summary>
public sealed class AgentUserTargetRootPolicy
{
    /// <summary> Initializes an immutable user-scope target policy. </summary>
    internal AgentUserTargetRootPolicy (
        string? environmentVariableName,
        RootRelativePath? environmentArtifactChildDirectory,
        RootRelativePath? environmentStateChildDirectory,
        RootRelativePath homeArtifactRelativeDirectory,
        RootRelativePath homeStateRelativeDirectory)
    {
        if (environmentVariableName is not null && string.IsNullOrWhiteSpace(environmentVariableName))
        {
            throw new ArgumentException("Environment variable name must be null or non-whitespace.", nameof(environmentVariableName));
        }

        if ((environmentArtifactChildDirectory is null) != (environmentStateChildDirectory is null))
        {
            throw new ArgumentException("Environment artifact and state child directories must be specified together.");
        }

        ArgumentNullException.ThrowIfNull(homeArtifactRelativeDirectory);
        ArgumentNullException.ThrowIfNull(homeStateRelativeDirectory);

        EnvironmentVariableName = environmentVariableName;
        EnvironmentArtifactChildDirectory = environmentArtifactChildDirectory;
        EnvironmentStateChildDirectory = environmentStateChildDirectory;
        HomeArtifactRelativeDirectory = homeArtifactRelativeDirectory;
        HomeStateRelativeDirectory = homeStateRelativeDirectory;
    }

    /// <summary> Gets the optional host-home override environment variable. </summary>
    public string? EnvironmentVariableName { get; }

    /// <summary> Gets the artifact directory appended to the environment-variable value. </summary>
    public RootRelativePath? EnvironmentArtifactChildDirectory { get; }

    /// <summary> Gets the state directory appended to the environment-variable value. </summary>
    public RootRelativePath? EnvironmentStateChildDirectory { get; }

    /// <summary> Gets the artifact directory relative to the user's home. </summary>
    public RootRelativePath HomeArtifactRelativeDirectory { get; }

    /// <summary> Gets the installation-state directory relative to the user's home. </summary>
    public RootRelativePath HomeStateRelativeDirectory { get; }
}
