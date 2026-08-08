using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Agents.Hosts;

/// <summary> Describes one agent host's target and installation-state layout. </summary>
public sealed class AgentHostDescriptor
{
    /// <summary> Initializes an immutable agent host descriptor. </summary>
    internal AgentHostDescriptor (
        AgentHostKind hostId,
        string projectDefaultArtifactRootPath,
        string projectDefaultStateRootPath,
        AgentUserTargetRootPolicy userTargetRootPolicy,
        string explicitTargetStateDirectoryName)
    {
        if (!Vocabulary.IsDefined(hostId))
        {
            throw new ArgumentOutOfRangeException(nameof(hostId), hostId, "Unsupported agent host.");
        }

        if (!PackageRelativePath.TryParse(projectDefaultArtifactRootPath, out _)
            || !PackageRelativePath.TryParse(projectDefaultStateRootPath, out _))
        {
            throw new ArgumentException("Default agent target paths must be safe relative paths.");
        }

        ArgumentNullException.ThrowIfNull(userTargetRootPolicy);
        if (!PackageRelativePath.TryParseSegment(explicitTargetStateDirectoryName, out _))
        {
            throw new ArgumentException("Explicit-target state directory name must be safe.", nameof(explicitTargetStateDirectoryName));
        }

        HostId = hostId;
        ProjectDefaultArtifactRootPath = projectDefaultArtifactRootPath;
        ProjectDefaultStateRootPath = projectDefaultStateRootPath;
        UserTargetRootPolicy = userTargetRootPolicy;
        ExplicitTargetStateDirectoryName = explicitTargetStateDirectoryName;
    }

    /// <summary> Gets the stable agent host identifier. </summary>
    public AgentHostKind HostId { get; }

    /// <summary> Gets the project-relative directory where the host discovers agent artifacts. </summary>
    public string ProjectDefaultArtifactRootPath { get; }

    /// <summary> Gets the project-relative directory reserved for Agent Skills installation state. </summary>
    public string ProjectDefaultStateRootPath { get; }

    /// <summary> Gets the user-scope target and state-root policy. </summary>
    public AgentUserTargetRootPolicy UserTargetRootPolicy { get; }

    /// <summary> Gets the hidden sibling directory used for state when an artifact target is explicit. </summary>
    public string ExplicitTargetStateDirectoryName { get; }
}
