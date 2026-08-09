using System.Globalization;
using MackySoft.AgentDistribution.Hosts.Registration;
using MackySoft.AgentDistribution.Installation.Validation;
using MackySoft.AgentDistribution.Packaging.Canonical;
using MackySoft.AgentDistribution.Shared;

namespace MackySoft.AgentDistribution.Tests.Materialization;

public sealed class SkillMaterializationServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Materialize_AllSkills_ForAllSupportedHosts_IsRepeatableAndOrdinalSorted ()
    {
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var service = SkillTestData.CreateMaterializationService();
        var registrations = GetSupportedHosts();

        foreach (var package in packages)
        {
            foreach (var registration in registrations)
            {
                var host = registration.Host;
                var adapter = registration.SkillAdapter;
                var first = service.Materialize(package, host);
                var second = service.Materialize(package, host);

                Assert.True(first.IsSuccess, first.Failure?.Message);
                Assert.True(second.IsSuccess, second.Failure?.Message);
                Assert.Equal(package.Manifest.SkillName.Value, first.Value!.SkillName.Value);
                Assert.Equal(host, first.Value.Host);
                Assert.Equal(
                    first.Value!.Files.Select(static file => (file.RelativePath, file.Content)),
                    second.Value!.Files.Select(static file => (file.RelativePath, file.Content)));
                var materializedFiles = first.Value.Files;
                var materializedPaths = materializedFiles.Select(static file => file.RelativePath).ToArray();

                Assert.Equal(materializedPaths.OrderBy(static path => path.Value, StringComparer.Ordinal).ToArray(), materializedPaths);
                Assert.Equal(GetExpectedPaths(package, adapter), materializedPaths);
            }
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Materialize_HostIndependentContent_MatchesCanonicalAcrossHosts ()
    {
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var service = SkillTestData.CreateMaterializationService();
        var registrations = GetSupportedHosts();

        foreach (var package in packages)
        {
            var canonicalContent = GetCanonicalHostIndependentContent(package);

            foreach (var registration in registrations)
            {
                var adapter = registration.SkillAdapter;
                var result = service.Materialize(package, registration.Host);

                Assert.True(result.IsSuccess, result.Failure?.Message);
                AssertFileMapEqual(canonicalContent, GetMaterializedHostIndependentContent(package, adapter, result.Value!.Files));
            }
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Materialize_UsesOrdinalOrdering_ForCultureSensitivePaths ()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            var package = SkillTestData.CreateOrdinalSensitivePackage();
            var service = SkillTestData.CreateMaterializationService();

            foreach (var registration in GetSupportedHosts())
            {
                var result = service.Materialize(package, registration.Host);

                Assert.True(result.IsSuccess, result.Failure?.Message);
                var materializedPaths = result.Value!.Files.Select(static file => file.RelativePath).ToArray();
                var ordinalPaths = materializedPaths.OrderBy(static path => path.Value, StringComparer.Ordinal).ToArray();

                Assert.Equal(ordinalPaths, materializedPaths);
                Assert.NotEqual(ordinalPaths, materializedPaths.OrderBy(static path => path.Value, StringComparer.CurrentCulture).ToArray());
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Materialize_EmitsOnlyRequestedHostMetadataArtifact ()
    {
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var service = SkillTestData.CreateMaterializationService();
        var registrations = GetSupportedHosts();

        foreach (var package in packages)
        {
            foreach (var registration in registrations)
            {
                var adapter = registration.SkillAdapter;
                var result = service.Materialize(package, registration.Host);

                Assert.True(result.IsSuccess, result.Failure?.Message);
                var hostArtifactPaths = GetHostArtifactPaths(package);
                var actualMetadataArtifactPaths = result.Value!.Files
                    .Select(static file => file.RelativePath)
                    .Where(hostArtifactPaths.Contains)
                    .ToArray();
                var metadataArtifactPath = adapter.Descriptor.MetadataArtifactPath;
                var expectedMetadataArtifactPaths = metadataArtifactPath is null ? [] : new[] { metadataArtifactPath };

                Assert.Equal(expectedMetadataArtifactPaths, actualMetadataArtifactPaths);

                if (metadataArtifactPath is not null)
                {
                    var expectedMetadata = adapter.BuildArtifacts(CreateHostMetadata(package)).MetadataContent;
                    var actualMetadata = result.Value.Files.Single(file => file.RelativePath.Equals(metadataArtifactPath)).Content;
                    Assert.Equal(expectedMetadata, actualMetadata);
                }
            }
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Materialize_ReturnsUnsupportedHostFailure_WhenHostIsUnknown ()
    {
        var package = (await SkillTestData.GenerateFixturePackagesAsync()).First();
        var service = SkillTestData.CreateMaterializationService();

        var result = service.Materialize(package, (HostKind)42);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.HostUnsupported, result.Failure!.Code);
    }

    private static IReadOnlyList<HostRegistration> GetSupportedHosts ()
    {
        return HostRegistration.Registrations;
    }

    private static PackageRelativePath[] GetExpectedPaths (
        CanonicalSkillPackage package,
        ISkillHostAdapter adapter)
    {
        var hostArtifactPaths = GetHostArtifactPaths(package);

        return package.Files
            .Where(file => !hostArtifactPaths.Contains(file.RelativePath))
            .Select(static file => file.RelativePath)
            .Concat(adapter.Descriptor.MetadataArtifactPath is null ? [] : [adapter.Descriptor.MetadataArtifactPath])
            .OrderBy(static path => path.Value, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyDictionary<PackageRelativePath, string> GetCanonicalHostIndependentContent (CanonicalSkillPackage package)
    {
        var hostArtifactPaths = GetHostArtifactPaths(package);

        return package.Files
            .Where(file => !hostArtifactPaths.Contains(file.RelativePath))
            .ToDictionary(static file => file.RelativePath, static file => file.Content);
    }

    private static IReadOnlyDictionary<PackageRelativePath, string> GetMaterializedHostIndependentContent (
        CanonicalSkillPackage package,
        ISkillHostAdapter adapter,
        IReadOnlyList<PackageTextFile> materializedFiles)
    {
        var hostArtifactPaths = GetHostArtifactPaths(package);
        var content = new Dictionary<PackageRelativePath, string>();
        var expectedFrontmatter = adapter.BuildArtifacts(CreateHostMetadata(package)).Frontmatter;

        foreach (var file in materializedFiles.Where(file => !hostArtifactPaths.Contains(file.RelativePath)))
        {
            if (string.Equals(file.RelativePath.Value, "SKILL.md", StringComparison.Ordinal))
            {
                Assert.True(SkillHostMaterializationInspector.TryExtractFrontmatter(file.Content, out var frontmatter));
                Assert.Equal(expectedFrontmatter, frontmatter);
                content.Add(file.RelativePath, GetBodyWithoutFrontmatter(file.Content, frontmatter));
                continue;
            }

            content.Add(file.RelativePath, file.Content);
        }

        return content;
    }

    private static HashSet<PackageRelativePath> GetHostArtifactPaths (CanonicalSkillPackage package)
    {
        return package.Manifest.HostArtifacts
            .Select(static artifact => artifact.Path)
            .Where(static path => path is not null)
            .Select(static path => path!)
            .ToHashSet();
    }

    private static SkillHostMetadata CreateHostMetadata (CanonicalSkillPackage package)
    {
        return new SkillHostMetadata(
            package.Manifest.SkillName,
            package.Manifest.DisplayName,
            package.Manifest.Description);
    }

    private static void AssertFileMapEqual (
        IReadOnlyDictionary<PackageRelativePath, string> expected,
        IReadOnlyDictionary<PackageRelativePath, string> actual)
    {
        var expectedPaths = expected.Keys.OrderBy(static path => path.Value, StringComparer.Ordinal).ToArray();
        var actualPaths = actual.Keys.OrderBy(static path => path.Value, StringComparer.Ordinal).ToArray();
        Assert.Equal(expectedPaths, actualPaths);

        foreach (var path in expectedPaths)
        {
            Assert.Equal(expected[path], actual[path]);
        }
    }

    private static string GetBodyWithoutFrontmatter (
        string skillText,
        string frontmatter)
    {
        var body = skillText[frontmatter.Length..];
        return body.StartsWith('\n') ? body[1..] : body;
    }
}
