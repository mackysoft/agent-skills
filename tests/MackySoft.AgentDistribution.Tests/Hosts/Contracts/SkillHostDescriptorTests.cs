namespace MackySoft.AgentDistribution.Tests.Hosts.Contracts;

public sealed class SkillHostDescriptorTests
{
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

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_RejectsMissingReloadGuidance ()
    {
        Assert.Throws<ArgumentException>(() => Create(reloadGuidance: " "));
    }

    private static SkillHostDescriptor Create (
        SkillBundleTargetRootLayout bundleTargetRootLayout = SkillBundleTargetRootLayout.CatalogDirectory,
        IReadOnlyList<SkillBundleTargetRootLayout>? compatiblePreviousBundleTargetRootLayouts = null,
        string reloadGuidance = "Reload skills.")
    {
        return new SkillHostDescriptor(
            RootRelativePath.Parse(".agents/skills"),
            new SkillUserTargetRootPolicy(null, null, RootRelativePath.Parse(".agents/skills")),
            bundleTargetRootLayout,
            compatiblePreviousBundleTargetRootLayouts ?? [SkillBundleTargetRootLayout.Flat],
            null,
            reloadGuidance);
    }
}
