using System.Reflection;
using MackySoft.AgentSkills.Hosts.Contracts;

namespace MackySoft.AgentSkills.Tests.Hosts.Contracts;

public sealed class SkillHostDescriptorTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void HostConfigurationTypes_DoNotExposeConstruction ()
    {
        Assert.Empty(typeof(SkillHostDescriptor).GetConstructors(BindingFlags.Instance | BindingFlags.Public));
        Assert.Empty(typeof(SkillUserTargetRootPolicy).GetConstructors(BindingFlags.Instance | BindingFlags.Public));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_RejectsUndefinedHost ()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(host: (SkillHostKind)42));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_RejectsUndefinedBundleTargetRootLayout ()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(bundleTargetRootLayout: (SkillBundleTargetRootLayout)42));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_RejectsInvalidCompatiblePreviousBundleTargetRootLayouts ()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(
            compatiblePreviousBundleTargetRootLayouts: [(SkillBundleTargetRootLayout)42]));
        Assert.Throws<ArgumentException>(() => Create(
            compatiblePreviousBundleTargetRootLayouts: [SkillBundleTargetRootLayout.CatalogDirectory]));
        Assert.Throws<ArgumentException>(() => Create(
            compatiblePreviousBundleTargetRootLayouts: [SkillBundleTargetRootLayout.Flat, SkillBundleTargetRootLayout.Flat]));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("../skills", null)]
    [InlineData(".agents/skills", "../openai.yaml")]
    public void Constructor_RejectsUnsafeRelativePaths (
        string projectDefaultTargetPath,
        string? metadataArtifactPath)
    {
        Assert.Throws<ArgumentException>(() => Create(
            projectDefaultTargetPath: projectDefaultTargetPath,
            metadataArtifactPath: metadataArtifactPath));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("", "Reload skills.")]
    [InlineData("~/.agents/skills", " ")]
    public void Constructor_RejectsMissingRequiredText (
        string userDefaultTargetPath,
        string reloadGuidance)
    {
        Assert.Throws<ArgumentException>(() => Create(
            userDefaultTargetPath: userDefaultTargetPath,
            reloadGuidance: reloadGuidance));
    }

    private static SkillHostDescriptor Create (
        SkillHostKind host = SkillHostKind.OpenAi,
        string projectDefaultTargetPath = ".agents/skills",
        string userDefaultTargetPath = "~/.agents/skills",
        SkillBundleTargetRootLayout bundleTargetRootLayout = SkillBundleTargetRootLayout.CatalogDirectory,
        IReadOnlyList<SkillBundleTargetRootLayout>? compatiblePreviousBundleTargetRootLayouts = null,
        string? metadataArtifactPath = null,
        string reloadGuidance = "Reload skills.")
    {
        return new SkillHostDescriptor(
            host,
            projectDefaultTargetPath,
            userDefaultTargetPath,
            new SkillUserTargetRootPolicy(null, null, ".agents/skills"),
            bundleTargetRootLayout,
            compatiblePreviousBundleTargetRootLayouts ?? [SkillBundleTargetRootLayout.Flat],
            metadataArtifactPath,
            reloadGuidance);
    }
}
