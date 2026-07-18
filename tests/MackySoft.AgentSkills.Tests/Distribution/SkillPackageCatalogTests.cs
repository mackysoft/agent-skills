using MackySoft.AgentSkills.Bundles;
using MackySoft.AgentSkills.Catalogs;
using MackySoft.AgentSkills.Categories;
using MackySoft.AgentSkills.Distribution;
using MackySoft.AgentSkills.Packaging.Canonical;

namespace MackySoft.AgentSkills.Tests.Distribution;

public sealed class SkillPackageCatalogTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Constructor_CapturesCollectionSnapshotsAndSortsPackages ()
    {
        var bundle = await SkillTestData.GenerateFixtureBundleAsync();
        var categories = new List<SkillCategory> { new("core") };
        var packages = bundle.Packages.Reverse().ToList();
        var catalog = new SkillPackageCatalog(
            bundle.Descriptor,
            categories,
            [],
            [new SkillCategoryPackageCount(categories[0], bundle.Packages.Count)],
            packages);

        categories.Clear();
        packages.Clear();

        Assert.Equal("core", Assert.Single(catalog.SelectedCategories).Value);
        Assert.Equal(
            SkillTestData.ExpectedSkillNames.Order(StringComparer.Ordinal),
            catalog.Packages.Select(static package => package.Manifest.SkillName.Value));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Constructor_SortsAvailableCategoriesByOrdinal ()
    {
        var bundle = await SkillTestData.GenerateFixtureBundleAsync();

        var catalog = new SkillPackageCatalog(
            bundle.Descriptor,
            [],
            [],
            [
                new SkillCategoryPackageCount(new SkillCategory("zeta"), 1),
                new SkillCategoryPackageCount(new SkillCategory("alpha"), 1),
            ],
            bundle.Packages);

        Assert.Equal(
            ["alpha", "zeta"],
            catalog.AvailableCategories.Select(static item => item.Category.Value));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Constructor_RejectsDuplicateAvailableCategoryValues ()
    {
        var bundle = await SkillTestData.GenerateFixtureBundleAsync();

        var exception = Assert.Throws<ArgumentException>(() => new SkillPackageCatalog(
            bundle.Descriptor,
            [],
            [],
            [
                new SkillCategoryPackageCount(new SkillCategory("duplicate"), 1),
                new SkillCategoryPackageCount(new SkillCategory("duplicate"), 2),
            ],
            bundle.Packages));

        Assert.Contains("duplicate", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Constructor_RejectsDuplicateSkillNameFromDistinctPackages ()
    {
        var bundle = await SkillTestData.GenerateFixtureBundleAsync();
        var package = bundle.Packages[0];
        var duplicateNamePackage = SkillTestData.CreateCanonicalPackage(package.Manifest, package.Files);
        Assert.NotSame(package, duplicateNamePackage);

        var exception = Assert.Throws<ArgumentException>(() => CreateCatalog(
            bundle.Descriptor,
            [package, duplicateNamePackage]));

        Assert.Contains(package.Manifest.SkillName.Value, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Constructor_RejectsPackageFromAnotherCatalog ()
    {
        var bundle = await SkillTestData.GenerateFixtureBundleAsync();
        var foreignDescriptor = new SkillBundleDescriptor(
            bundle.Descriptor.SchemaVersion,
            new SkillCatalogId("foreign-catalog"),
            bundle.Descriptor.SkillBundleVersion,
            bundle.Descriptor.BundleDigest);

        var exception = Assert.Throws<ArgumentException>(() => CreateCatalog(foreignDescriptor, bundle.Packages));

        Assert.Contains(bundle.Packages[0].Manifest.SkillName.Value, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Constructor_RejectsPackageFromAnotherBundleVersion ()
    {
        var bundle = await SkillTestData.GenerateFixtureBundleAsync();
        var foreignDescriptor = new SkillBundleDescriptor(
            bundle.Descriptor.SchemaVersion,
            bundle.Descriptor.CatalogId,
            bundle.Descriptor.SkillBundleVersion.Next(),
            bundle.Descriptor.BundleDigest);

        var exception = Assert.Throws<ArgumentException>(() => CreateCatalog(foreignDescriptor, bundle.Packages));

        Assert.Contains(bundle.Packages[0].Manifest.SkillName.Value, exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [Trait("Size", "Small")]
    public void CategoryPackageCount_RejectsNonPositiveCount (int packageCount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SkillCategoryPackageCount(
            new SkillCategory("core"),
            packageCount));
    }

    private static SkillPackageCatalog CreateCatalog (
        SkillBundleDescriptor descriptor,
        IReadOnlyList<CanonicalSkillPackage> packages)
    {
        var availableCategories = packages
            .GroupBy(static package => package.Manifest.Category)
            .Select(static group => new SkillCategoryPackageCount(group.Key, group.Count()))
            .ToArray();

        return new SkillPackageCatalog(
            descriptor,
            availableCategories.Select(static item => item.Category).ToArray(),
            [],
            availableCategories,
            packages);
    }
}
