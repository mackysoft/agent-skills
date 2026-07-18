using MackySoft.AgentSkills.Catalogs;
using MackySoft.AgentSkills.Categories;
using MackySoft.AgentSkills.Hosts.Contracts;
using MackySoft.AgentSkills.Installation.Requests;
using MackySoft.AgentSkills.Installation.Targeting;
using MackySoft.AgentSkills.Manifests;
using MackySoft.AgentSkills.Names;
using MackySoft.AgentSkills.Packaging.Canonical;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Tests.Installation.Requests;

public sealed class SkillInstallationInputTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void InstallInput_CapturesPackageSnapshot ()
    {
        var packages = new List<CanonicalSkillPackage>();
        var catalogId = new SkillCatalogId("catalog");
        var input = new SkillInstallInput(catalogId, packages, CreateTargetRequest());

        packages.Add(null!);

        Assert.Empty(input.Packages);
        Assert.Equal(catalogId, input.CatalogId);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void InstallInput_RejectsNullPackageItem ()
    {
        Assert.Throws<ArgumentException>(() => new SkillInstallInput(
            new SkillCatalogId("catalog"),
            [null!],
            CreateTargetRequest()));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InstallInput_RejectsDuplicateSkillNameFromDistinctPackages ()
    {
        var packages = await CreateDistinctPackagesWithSameSkillNameAsync();

        var exception = Assert.Throws<ArgumentException>(() => new SkillInstallInput(
            packages[0].Manifest.CatalogId,
            packages,
            CreateTargetRequest()));

        Assert.Contains(packages[0].Manifest.SkillName.Value, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateInput_RejectsDuplicateSkillNameFromDistinctPackages ()
    {
        var packages = await CreateDistinctPackagesWithSameSkillNameAsync();

        var exception = Assert.Throws<ArgumentException>(() => new SkillUpdateInput(
            packages[0].Manifest.CatalogId,
            packages,
            CreateTargetRequest()));

        Assert.Contains(packages[0].Manifest.SkillName.Value, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UninstallInput_RejectsDuplicateSkillNameFromDistinctPackages ()
    {
        var packages = await CreateDistinctPackagesWithSameSkillNameAsync();

        var exception = Assert.Throws<ArgumentException>(() => new SkillUninstallInput(
            packages[0].Manifest.CatalogId,
            packages,
            CreateTargetRequest()));

        Assert.Contains(packages[0].Manifest.SkillName.Value, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void PruneInput_CapturesFilterSnapshots ()
    {
        var categories = new List<SkillCategory> { new("core") };
        var names = new List<SkillName> { new("skill-a") };
        var input = new SkillPruneInput(
            new SkillCatalogId("catalog"),
            [],
            CreateTargetRequest(),
            SelectedCategories: categories,
            SelectedSkillNames: names);

        categories.Clear();
        names.Clear();

        Assert.Equal("core", Assert.Single(input.SelectedCategories!).Value);
        Assert.Equal("skill-a", Assert.Single(input.SelectedSkillNames!).Value);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void PruneInput_RejectsNullSkillNameFilter ()
    {
        Assert.Throws<ArgumentException>(() => new SkillPruneInput(
            new SkillCatalogId("catalog"),
            [],
            CreateTargetRequest(),
            SelectedSkillNames: [null!]));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task PruneInput_RejectsPackageFromAnotherCatalog ()
    {
        var package = (await SkillTestData.GenerateFixturePackagesAsync())[0];
        var foreignPackage = CopyPackageWithCatalogId(package, new SkillCatalogId("foreign-catalog"));

        Assert.Throws<ArgumentException>(() => new SkillPruneInput(
            package.Manifest.CatalogId,
            [foreignPackage],
            CreateTargetRequest()));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task OperationInputs_RejectPackageFromAnotherCatalog ()
    {
        var package = (await SkillTestData.GenerateFixturePackagesAsync())[0];
        var foreignPackage = CopyPackageWithCatalogId(package, new SkillCatalogId("foreign-catalog"));
        var request = CreateTargetRequest();

        Assert.Throws<ArgumentException>(() => new SkillInstallInput(package.Manifest.CatalogId, [foreignPackage], request));
        Assert.Throws<ArgumentException>(() => new SkillUpdateInput(package.Manifest.CatalogId, [foreignPackage], request));
        Assert.Throws<ArgumentException>(() => new SkillUninstallInput(package.Manifest.CatalogId, [foreignPackage], request));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task PruneInput_RejectsDuplicateSkillNames ()
    {
        var package = (await SkillTestData.GenerateFixturePackagesAsync())[0];

        Assert.Throws<ArgumentException>(() => new SkillPruneInput(
            package.Manifest.CatalogId,
            [package, package],
            CreateTargetRequest()));
    }

    private static SkillInstallRequest CreateTargetRequest ()
    {
        return new SkillInstallRequest(SkillHostKind.OpenAi, SkillScopeKind.Project, Path.GetFullPath("repository"));
    }

    private static async Task<IReadOnlyList<CanonicalSkillPackage>> CreateDistinctPackagesWithSameSkillNameAsync ()
    {
        var package = (await SkillTestData.GenerateFixturePackagesAsync())[0];
        var duplicateNamePackage = SkillTestData.CreateCanonicalPackage(package.Manifest, package.Files);

        Assert.NotSame(package, duplicateNamePackage);
        return [package, duplicateNamePackage];
    }

    private static CanonicalSkillPackage CopyPackageWithCatalogId (
        CanonicalSkillPackage package,
        SkillCatalogId catalogId)
    {
        var foreignManifest = SkillTestData.CopyManifest(package.Manifest, catalogId: catalogId);
        var foreignCanonicalManifest = SkillTestData.WithComputedManifestDigest(foreignManifest);
        var manifestText = new SkillManifestJsonSerializer().Serialize(foreignCanonicalManifest);
        var foreignFiles = package.Files
            .Select(file => string.Equals(file.RelativePath, "agent-skill.json", StringComparison.Ordinal)
                ? new SkillPackageFile(file.RelativePath, manifestText)
                : file)
            .ToArray();
        return SkillTestData.CreateCanonicalPackage(foreignCanonicalManifest, foreignFiles);
    }
}
