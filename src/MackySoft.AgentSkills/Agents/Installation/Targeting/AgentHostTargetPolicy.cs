using MackySoft.FileSystem;

namespace MackySoft.AgentSkills.Agents.Installation.Targeting;

/// <summary>Describes one agent host's target and installation-state layout.</summary>
public sealed class AgentHostTargetPolicy
{
    /// <summary>Initializes an immutable agent host target policy.</summary>
    internal AgentHostTargetPolicy (
        RootRelativePath projectDefaultArtifactRootPath,
        RootRelativePath projectDefaultStateRootPath,
        AgentUserTargetRootPolicy userTargetRootPolicy,
        RootRelativePath explicitTargetStateDirectory)
    {
        ArgumentNullException.ThrowIfNull(projectDefaultArtifactRootPath);
        ArgumentNullException.ThrowIfNull(projectDefaultStateRootPath);
        ArgumentNullException.ThrowIfNull(userTargetRootPolicy);
        ArgumentNullException.ThrowIfNull(explicitTargetStateDirectory);
        if (explicitTargetStateDirectory.IsRoot
            || explicitTargetStateDirectory.Value.Contains('/', StringComparison.Ordinal))
        {
            throw new ArgumentException("Explicit-target state directory must be one non-navigation segment.", nameof(explicitTargetStateDirectory));
        }

        ProjectDefaultArtifactRootPath = projectDefaultArtifactRootPath;
        ProjectDefaultStateRootPath = projectDefaultStateRootPath;
        UserTargetRootPolicy = userTargetRootPolicy;
        ExplicitTargetStateDirectory = explicitTargetStateDirectory;
    }

    /// <summary>Gets the project-relative directory where the host discovers agent artifacts.</summary>
    public RootRelativePath ProjectDefaultArtifactRootPath { get; }

    /// <summary>Gets the project-relative directory reserved for Agent Skills installation state.</summary>
    public RootRelativePath ProjectDefaultStateRootPath { get; }

    /// <summary>Gets the user-scope target and state-root policy.</summary>
    public AgentUserTargetRootPolicy UserTargetRootPolicy { get; }

    /// <summary>Gets the hidden sibling directory used for state when an artifact target is explicit.</summary>
    public RootRelativePath ExplicitTargetStateDirectory { get; }
}
