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
        string bundleTargetRootLayout,
        IReadOnlyList<string> compatiblePreviousBundleTargetRootLayouts,
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
        if (!ContractLiteralCodec.IsDefined<SkillBundleTargetRootLayout>(bundleTargetRootLayout))
        {
            throw new ArgumentException("Unsupported bundle target-root layout.", nameof(bundleTargetRootLayout));
        }

        ArgumentNullException.ThrowIfNull(compatiblePreviousBundleTargetRootLayouts);
        var previousLayouts = compatiblePreviousBundleTargetRootLayouts.ToArray();
        if (previousLayouts.Any(layout => !ContractLiteralCodec.IsDefined<SkillBundleTargetRootLayout>(layout)))
        {
            throw new ArgumentException(
                "Compatible previous bundle target-root layouts must be supported.",
                nameof(compatiblePreviousBundleTargetRootLayouts));
        }

        if (previousLayouts.Contains(bundleTargetRootLayout, StringComparer.Ordinal)
            || previousLayouts.Distinct(StringComparer.Ordinal).Count() != previousLayouts.Length)
        {
            throw new ArgumentException(
                "Compatible previous bundle target-root layouts must be unique and must not contain the current layout.",
                nameof(compatiblePreviousBundleTargetRootLayouts));
        }

        if (metadataArtifactPath is not null)
        {
            OperationReportContractGuard.ValidateSafeRelativePath(metadataArtifactPath, nameof(metadataArtifactPath));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(reloadGuidance);

        Host = host;
        ProjectDefaultTargetPath = projectDefaultTargetPath;
        UserDefaultTargetPath = userDefaultTargetPath;
        UserTargetRootPolicy = userTargetRootPolicy;
        BundleTargetRootLayout = bundleTargetRootLayout;
        CompatiblePreviousBundleTargetRootLayouts = Array.AsReadOnly(previousLayouts);
        MetadataArtifactPath = metadataArtifactPath;
        ReloadGuidance = reloadGuidance;
    }

    /// <summary> Gets the supported host. </summary>
    public SkillHostKind Host { get; }

    /// <summary> Gets the project-scope default host SKILL root path relative to repository root. </summary>
    public string ProjectDefaultTargetPath { get; }

    /// <summary> Gets the user-scope default host SKILL root path shown to users. </summary>
    public string UserDefaultTargetPath { get; }

    /// <summary> Gets the user-scope host SKILL root resolution policy. </summary>
    public SkillUserTargetRootPolicyReport UserTargetRootPolicy { get; }

    /// <summary> Gets the layout used for a catalog that has no compatible existing installation. </summary>
    public string BundleTargetRootLayout { get; }

    /// <summary> Gets previous default layouts that remain valid for existing managed catalogs. </summary>
    public IReadOnlyList<string> CompatiblePreviousBundleTargetRootLayouts { get; }

    /// <summary> Gets the metadata artifact path relative to each skill directory, or <see langword="null" /> for frontmatter-only hosts. </summary>
    public string? MetadataArtifactPath { get; }

    /// <summary> Gets the host-specific guidance for reloading installed SKILLs. </summary>
    public string ReloadGuidance { get; }
}
