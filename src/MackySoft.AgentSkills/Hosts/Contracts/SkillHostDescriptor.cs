using MackySoft.AgentSkills.Shared;
using MackySoft.AgentSkills.Shared.Text;

namespace MackySoft.AgentSkills.Hosts.Contracts;

/// <summary> Describes one supported SKILL host and its install/materialization policy. </summary>
public sealed class SkillHostDescriptor
{
    /// <summary> Initializes one immutable host descriptor. </summary>
    internal SkillHostDescriptor (
        SkillHostKind host,
        string projectDefaultTargetPath,
        string userDefaultTargetPath,
        SkillUserTargetRootPolicy userTargetRootPolicy,
        string? metadataArtifactPath,
        string reloadGuidance)
    {
        if (!ContractLiteralCodec.IsDefined(host))
        {
            throw new ArgumentOutOfRangeException(nameof(host), host, "Unsupported SKILL host value.");
        }

        if (!SkillRelativePath.IsSafeFilePath(projectDefaultTargetPath))
        {
            throw new ArgumentException("Project default target path must be a safe relative path.", nameof(projectDefaultTargetPath));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(userDefaultTargetPath);
        ArgumentNullException.ThrowIfNull(userTargetRootPolicy);
        if (metadataArtifactPath is not null && !SkillRelativePath.IsSafeFilePath(metadataArtifactPath))
        {
            throw new ArgumentException("Metadata artifact path must be a safe relative path.", nameof(metadataArtifactPath));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(reloadGuidance);

        Host = host;
        ProjectDefaultTargetPath = projectDefaultTargetPath;
        UserDefaultTargetPath = userDefaultTargetPath;
        UserTargetRootPolicy = userTargetRootPolicy;
        MetadataArtifactPath = metadataArtifactPath;
        ReloadGuidance = reloadGuidance;
    }

    /// <summary> Gets the supported host. </summary>
    public SkillHostKind Host { get; }

    /// <summary> Gets the project-scope default target path relative to the repository root. </summary>
    public string ProjectDefaultTargetPath { get; }

    /// <summary> Gets the user-scope default target path shown to users. </summary>
    public string UserDefaultTargetPath { get; }

    /// <summary> Gets the user-scope target root resolution policy. </summary>
    public SkillUserTargetRootPolicy UserTargetRootPolicy { get; }

    /// <summary> Gets the metadata artifact path, or <see langword="null" /> when the host uses frontmatter only. </summary>
    public string? MetadataArtifactPath { get; }

    /// <summary> Gets host-specific guidance for reloading installed SKILLs. </summary>
    public string ReloadGuidance { get; }
}
