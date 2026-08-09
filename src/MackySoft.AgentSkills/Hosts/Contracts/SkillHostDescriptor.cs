using MackySoft.AgentSkills.Shared;
using MackySoft.FileSystem;

namespace MackySoft.AgentSkills.Hosts.Contracts;

/// <summary> Describes one supported SKILL host and its install/materialization policy. </summary>
public sealed class SkillHostDescriptor
{
    /// <summary> Initializes one immutable host descriptor. </summary>
    internal SkillHostDescriptor (
        RootRelativePath projectDefaultTargetPath,
        SkillUserTargetRootPolicy userTargetRootPolicy,
        SkillBundleTargetRootLayout bundleTargetRootLayout,
        IReadOnlyList<SkillBundleTargetRootLayout> compatiblePreviousBundleTargetRootLayouts,
        PackageRelativePath? metadataArtifactPath,
        string reloadGuidance)
    {
        ArgumentNullException.ThrowIfNull(projectDefaultTargetPath);
        ArgumentNullException.ThrowIfNull(userTargetRootPolicy);
        if (!Vocabulary.IsDefined(bundleTargetRootLayout))
        {
            throw new ArgumentOutOfRangeException(nameof(bundleTargetRootLayout), bundleTargetRootLayout, "Unsupported bundle target-root layout.");
        }

        ArgumentNullException.ThrowIfNull(compatiblePreviousBundleTargetRootLayouts);
        var previousLayouts = compatiblePreviousBundleTargetRootLayouts.ToArray();
        if (previousLayouts.Any(layout => !Vocabulary.IsDefined(layout)))
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

        ArgumentException.ThrowIfNullOrWhiteSpace(reloadGuidance);

        ProjectDefaultTargetPath = projectDefaultTargetPath;
        UserTargetRootPolicy = userTargetRootPolicy;
        BundleTargetRootLayout = bundleTargetRootLayout;
        CompatiblePreviousBundleTargetRootLayouts = Array.AsReadOnly(previousLayouts);
        MetadataArtifactPath = metadataArtifactPath;
        ReloadGuidance = reloadGuidance;
    }

    /// <summary> Gets the project-scope default host SKILL root path relative to the repository root. </summary>
    public RootRelativePath ProjectDefaultTargetPath { get; }

    /// <summary> Gets the user-scope host SKILL root resolution policy. </summary>
    public SkillUserTargetRootPolicy UserTargetRootPolicy { get; }

    /// <summary> Gets how the host organizes default bundle targets under its SKILL root. </summary>
    public SkillBundleTargetRootLayout BundleTargetRootLayout { get; }

    /// <summary> Gets previous default layouts whose managed installations remain valid operation targets. </summary>
    public IReadOnlyList<SkillBundleTargetRootLayout> CompatiblePreviousBundleTargetRootLayouts { get; }

    /// <summary> Gets the metadata artifact path, or <see langword="null" /> when the host uses frontmatter only. </summary>
    public PackageRelativePath? MetadataArtifactPath { get; }

    /// <summary> Gets host-specific guidance for reloading installed SKILLs. </summary>
    public string ReloadGuidance { get; }
}
