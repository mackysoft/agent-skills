using MackySoft.AgentSkills.Hosts.Contracts;
using MackySoft.AgentSkills.Shared.Text;

namespace MackySoft.AgentSkills.OperationReports.Contracts;

/// <summary> Represents product-neutral capability data for one supported SKILL host. </summary>
public sealed class SkillHostReport
{
    internal SkillHostReport (
        SkillHostKind host,
        string projectDefaultTargetPath,
        string userDefaultTargetPath,
        SkillUserTargetRootPolicyReport userTargetRootPolicy,
        string? metadataArtifactPath,
        string reloadGuidance)
    {
        if (!ContractLiteralCodec.IsDefined(host))
        {
            throw new ArgumentOutOfRangeException(nameof(host), host, "Unsupported SKILL host.");
        }

        OperationReportContractGuard.ValidateSafeRelativePath(projectDefaultTargetPath, nameof(projectDefaultTargetPath));
        ArgumentException.ThrowIfNullOrWhiteSpace(userDefaultTargetPath);
        ArgumentNullException.ThrowIfNull(userTargetRootPolicy);
        if (metadataArtifactPath is not null)
        {
            OperationReportContractGuard.ValidateSafeRelativePath(metadataArtifactPath, nameof(metadataArtifactPath));
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

    /// <summary> Gets the project-scope default target path relative to repository root. </summary>
    public string ProjectDefaultTargetPath { get; }

    /// <summary> Gets the user-scope default target path shown to users. </summary>
    public string UserDefaultTargetPath { get; }

    /// <summary> Gets the user-scope target root resolution policy. </summary>
    public SkillUserTargetRootPolicyReport UserTargetRootPolicy { get; }

    /// <summary> Gets the metadata artifact path relative to each skill directory, or <see langword="null" /> for frontmatter-only hosts. </summary>
    public string? MetadataArtifactPath { get; }

    /// <summary> Gets the host-specific guidance for reloading installed SKILLs. </summary>
    public string ReloadGuidance { get; }
}
