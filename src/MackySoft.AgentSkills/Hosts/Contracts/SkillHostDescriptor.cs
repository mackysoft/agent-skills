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
        SkillBundleTargetRootLayout bundleTargetRootLayout,
        IReadOnlyList<SkillBundleTargetRootLayout> compatiblePreviousBundleTargetRootLayouts,
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
        if (!ContractLiteralCodec.IsDefined(bundleTargetRootLayout))
        {
            throw new ArgumentOutOfRangeException(nameof(bundleTargetRootLayout), bundleTargetRootLayout, "Unsupported bundle target-root layout.");
        }

        ArgumentNullException.ThrowIfNull(compatiblePreviousBundleTargetRootLayouts);
        var previousLayouts = compatiblePreviousBundleTargetRootLayouts.ToArray();
        if (previousLayouts.Any(layout => !ContractLiteralCodec.IsDefined(layout)))
        {
            throw new ArgumentOutOfRangeException(
                nameof(compatiblePreviousBundleTargetRootLayouts),
                compatiblePreviousBundleTargetRootLayouts,
                "Compatible previous bundle target-root layouts must be supported.");
        }

        if (previousLayouts.Contains(bundleTargetRootLayout) || previousLayouts.Distinct().Count() != previousLayouts.Length)
        {
            throw new ArgumentException(
                "Compatible previous bundle target-root layouts must be unique and must not contain the current layout.",
                nameof(compatiblePreviousBundleTargetRootLayouts));
        }

        if (metadataArtifactPath is not null && !SkillRelativePath.IsSafeFilePath(metadataArtifactPath))
        {
            throw new ArgumentException("Metadata artifact path must be a safe relative path.", nameof(metadataArtifactPath));
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

    /// <summary> Gets the project-scope default host SKILL root path relative to the repository root. </summary>
    public string ProjectDefaultTargetPath { get; }

    /// <summary> Gets the user-scope default host SKILL root path shown to users. </summary>
    public string UserDefaultTargetPath { get; }

    /// <summary> Gets the user-scope host SKILL root resolution policy. </summary>
    public SkillUserTargetRootPolicy UserTargetRootPolicy { get; }

    /// <summary> Gets how the host organizes default bundle targets under its SKILL root. </summary>
    public SkillBundleTargetRootLayout BundleTargetRootLayout { get; }

    /// <summary> Gets previous default layouts whose managed installations remain valid operation targets. </summary>
    public IReadOnlyList<SkillBundleTargetRootLayout> CompatiblePreviousBundleTargetRootLayouts { get; }

    /// <summary> Gets the metadata artifact path, or <see langword="null" /> when the host uses frontmatter only. </summary>
    public string? MetadataArtifactPath { get; }

    /// <summary> Gets host-specific guidance for reloading installed SKILLs. </summary>
    public string ReloadGuidance { get; }
}
