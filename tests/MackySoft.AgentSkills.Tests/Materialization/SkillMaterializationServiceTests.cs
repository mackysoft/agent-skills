using System.Globalization;
using MackySoft.AgentSkills.Hosts.Contracts;
using MackySoft.AgentSkills.Installation.Validation;
using MackySoft.AgentSkills.Packaging.Canonical;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Tests.Materialization;

public sealed class SkillMaterializationServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Materialize_AllSkills_ForAllSupportedHosts_IsRepeatableAndOrdinalSorted ()
    {
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var service = SkillTestData.CreateMaterializationService();
        var adapters = GetSupportedAdapters();

        foreach (var package in packages)
        {
            foreach (var adapter in adapters)
            {
                var host = adapter.Descriptor.HostKey;
                var first = service.Materialize(package, host);
                var second = service.Materialize(package, host);

                Assert.True(first.IsSuccess, first.Failure?.Message);
                Assert.True(second.IsSuccess, second.Failure?.Message);
                Assert.Equal(package.Manifest.SkillName, first.Value!.SkillName);
                Assert.Equal(host, first.Value.Host);
                Assert.Equal(first.Value!.Files, second.Value!.Files);
                var materializedFiles = first.Value.Files;
                var materializedPaths = materializedFiles.Select(static file => file.RelativePath).ToArray();

                Assert.Equal(materializedPaths.Order(StringComparer.Ordinal).ToArray(), materializedPaths);
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
        var adapters = GetSupportedAdapters();

        foreach (var package in packages)
        {
            var canonicalContent = GetCanonicalHostIndependentContent(package);

            foreach (var adapter in adapters)
            {
                var result = service.Materialize(package, adapter.Descriptor.HostKey);

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

            foreach (var adapter in GetSupportedAdapters())
            {
                var result = service.Materialize(package, adapter.Descriptor.HostKey);

                Assert.True(result.IsSuccess, result.Failure?.Message);
                var materializedPaths = result.Value!.Files.Select(static file => file.RelativePath).ToArray();
                var ordinalPaths = materializedPaths.Order(StringComparer.Ordinal).ToArray();

                Assert.Equal(ordinalPaths, materializedPaths);
                Assert.NotEqual(ordinalPaths, materializedPaths.Order(StringComparer.CurrentCulture).ToArray());
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
        var adapters = GetSupportedAdapters();

        foreach (var package in packages)
        {
            foreach (var adapter in adapters)
            {
                var result = service.Materialize(package, adapter.Descriptor.HostKey);

                Assert.True(result.IsSuccess, result.Failure?.Message);
                var hostArtifactPaths = GetHostArtifactPaths(package);
                var actualMetadataArtifactPaths = result.Value!.Files
                    .Select(static file => file.RelativePath)
                    .Where(hostArtifactPaths.Contains)
                    .ToArray();
                var expectedMetadataArtifactPaths = adapter.MetadataArtifactPath is null ? [] : new[] { adapter.MetadataArtifactPath };

                Assert.Equal(expectedMetadataArtifactPaths, actualMetadataArtifactPaths);

                if (adapter.MetadataArtifactPath is not null)
                {
                    var expectedMetadata = adapter.BuildArtifacts(CreateHostMetadata(package)).MetadataContent;
                    var actualMetadata = result.Value.Files.Single(file => string.Equals(file.RelativePath, adapter.MetadataArtifactPath, StringComparison.Ordinal)).Content;
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

        var result = service.Materialize(package, "generic");

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.HostUnsupported, result.Failure!.Code);
    }

    private static IReadOnlyList<ISkillHostAdapter> GetSupportedAdapters ()
    {
        return SkillTestData.CreateDefaultHostAdapterSet().Adapters;
    }

    private static string[] GetExpectedPaths (
        CanonicalSkillPackage package,
        ISkillHostAdapter adapter)
    {
        var hostArtifactPaths = GetHostArtifactPaths(package);

        return package.Files
            .Where(file => !hostArtifactPaths.Contains(file.RelativePath))
            .Select(static file => file.RelativePath)
            .Concat(adapter.MetadataArtifactPath is null ? [] : [adapter.MetadataArtifactPath])
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyDictionary<string, string> GetCanonicalHostIndependentContent (CanonicalSkillPackage package)
    {
        var hostArtifactPaths = GetHostArtifactPaths(package);

        return package.Files
            .Where(file => !hostArtifactPaths.Contains(file.RelativePath))
            .ToDictionary(static file => file.RelativePath, static file => file.Content, StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, string> GetMaterializedHostIndependentContent (
        CanonicalSkillPackage package,
        ISkillHostAdapter adapter,
        IReadOnlyList<SkillPackageFile> materializedFiles)
    {
        var hostArtifactPaths = GetHostArtifactPaths(package);
        var content = new Dictionary<string, string>(StringComparer.Ordinal);
        var expectedFrontmatter = adapter.BuildArtifacts(CreateHostMetadata(package)).Frontmatter;

        foreach (var file in materializedFiles.Where(file => !hostArtifactPaths.Contains(file.RelativePath)))
        {
            if (string.Equals(file.RelativePath, "SKILL.md", StringComparison.Ordinal))
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

    private static HashSet<string> GetHostArtifactPaths (CanonicalSkillPackage package)
    {
        return package.Manifest.HostArtifacts
            .Select(static artifact => artifact.Path)
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Select(static path => path!)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static SkillHostMetadata CreateHostMetadata (CanonicalSkillPackage package)
    {
        return new SkillHostMetadata(
            package.Manifest.SkillName,
            package.Manifest.DisplayName,
            package.Manifest.Description);
    }

    private static void AssertFileMapEqual (
        IReadOnlyDictionary<string, string> expected,
        IReadOnlyDictionary<string, string> actual)
    {
        var expectedPaths = expected.Keys.Order(StringComparer.Ordinal).ToArray();
        var actualPaths = actual.Keys.Order(StringComparer.Ordinal).ToArray();
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
