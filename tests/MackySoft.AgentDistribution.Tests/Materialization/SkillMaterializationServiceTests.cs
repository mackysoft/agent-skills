using System.Globalization;
using MackySoft.AgentDistribution.Digests;
using MackySoft.AgentDistribution.Hosts.Registration;
using MackySoft.AgentDistribution.Shared;
using MackySoft.AgentDistribution.Skills.Manifests;
using MackySoft.AgentDistribution.Skills.Packaging.Canonical;

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
    public async Task Materialize_PreservesHostIndependentPackageFilesAcrossHosts ()
    {
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var service = SkillTestData.CreateMaterializationService();
        var registrations = GetSupportedHosts();

        foreach (var package in packages)
        {
            foreach (var registration in registrations)
            {
                var result = service.Materialize(package, registration.Host);

                Assert.True(result.IsSuccess, result.Failure?.Message);
                AssertHostIndependentPackageFilesPreserved(package, result.Value!.Files);
            }
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Materialize_PreservesNestedScriptsAcrossHosts ()
    {
        var generated = (await SkillTestData.GenerateFixturePackagesAsync())[0];
        var package = SkillTestData.CreatePackageWithScripts(
            generated,
            [new PackageTextFile(PackageRelativePath.Parse("scripts/bench/collect.sh"), "#!/bin/sh\necho collect\n")]);
        var service = SkillTestData.CreateMaterializationService();

        foreach (var registration in GetSupportedHosts())
        {
            var result = service.Materialize(package, registration.Host);

            Assert.True(result.IsSuccess, result.Failure?.Message);
            var script = Assert.Single(result.Value!.Files, static file => file.RelativePath.Value == "scripts/bench/collect.sh");
            Assert.Equal("#!/bin/sh\necho collect\n", script.Content);
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
        Assert.Equal(AgentDistributionFailureCodes.HostUnsupported, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Materialize_RejectsFrontmatterDigestThatDoesNotMatchCurrentAdapter ()
    {
        var package = (await SkillTestData.GenerateFixturePackagesAsync())[0];
        var incompatiblePackage = SkillTestData.CreatePackageWithDeclaredFrontmatterDigest(
            package,
            HostKind.ClaudeCode,
            Sha256Digest.Parse(new string('0', 64)));
        var service = SkillTestData.CreateMaterializationService();

        var result = service.Materialize(incompatiblePackage, HostKind.ClaudeCode);

        Assert.False(result.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.ManifestInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Materialize_RejectsMetadataContentThatDoesNotMatchCurrentAdapter ()
    {
        var package = (await SkillTestData.GenerateFixturePackagesAsync())[0];
        var incompatiblePackage = CreatePackageWithDeclaredCodexMetadata(
            package,
            "interface:\n  display_name: Incompatible\n");
        var service = SkillTestData.CreateMaterializationService();

        var result = service.Materialize(incompatiblePackage, HostKind.Codex);

        Assert.False(result.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.ManifestInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Materialize_RejectsMetadataPathThatDoesNotMatchCurrentRegistration ()
    {
        var package = (await SkillTestData.GenerateFixturePackagesAsync())[0];
        var incompatiblePackage = CreatePackageWithDeclaredClaudeMetadata(
            package,
            PackageRelativePath.Parse("claude.yaml"),
            "declared metadata\n");
        var service = SkillTestData.CreateMaterializationService();

        var result = service.Materialize(incompatiblePackage, HostKind.ClaudeCode);

        Assert.False(result.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.ManifestInvalid, result.Failure!.Code);
    }

    private static IReadOnlyList<HostRegistration> GetSupportedHosts ()
    {
        return BuiltInHostCatalog.Registrations;
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

    private static void AssertHostIndependentPackageFilesPreserved (
        CanonicalSkillPackage package,
        IReadOnlyList<PackageTextFile> materializedFiles)
    {
        foreach (var expectedFile in package.Files.Where(static file => IsHostIndependentPackageFile(file.RelativePath)))
        {
            var actualFile = Assert.Single(materializedFiles, file => file.RelativePath == expectedFile.RelativePath);
            if (string.Equals(expectedFile.RelativePath.Value, "SKILL.md", StringComparison.Ordinal))
            {
                Assert.EndsWith("\n" + expectedFile.Content, actualFile.Content, StringComparison.Ordinal);
                continue;
            }

            Assert.Equal(expectedFile.Content, actualFile.Content);
        }
    }

    private static bool IsHostIndependentPackageFile (PackageRelativePath path)
    {
        return string.Equals(path.Value, "SKILL.md", StringComparison.Ordinal)
            || string.Equals(path.Value, "agent-skill.json", StringComparison.Ordinal)
            || path.Value.StartsWith("references/", StringComparison.Ordinal)
            || path.Value.StartsWith("scripts/", StringComparison.Ordinal);
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

    private static CanonicalSkillPackage CreatePackageWithDeclaredCodexMetadata (
        CanonicalSkillPackage package,
        string metadataContent)
    {
        var metadataPath = PackageRelativePath.Parse("agents/openai.yaml");
        return CreatePackageWithDeclaredMetadata(package, HostKind.Codex, metadataPath, metadataContent);
    }

    private static CanonicalSkillPackage CreatePackageWithDeclaredClaudeMetadata (
        CanonicalSkillPackage package,
        PackageRelativePath metadataPath,
        string metadataContent)
    {
        return CreatePackageWithDeclaredMetadata(package, HostKind.ClaudeCode, metadataPath, metadataContent);
    }

    private static CanonicalSkillPackage CreatePackageWithDeclaredMetadata (
        CanonicalSkillPackage package,
        HostKind host,
        PackageRelativePath metadataPath,
        string metadataContent)
    {
        var digestCalculator = new PackageContentDigestCalculator();
        var metadataDigest = digestCalculator.ComputeSingleFileDigest(metadataPath, metadataContent);
        var manifestCandidate = SkillTestData.CopyManifest(
            package.Manifest,
            hostArtifacts: package.Manifest.HostArtifacts
                .Select(artifact => artifact.Host == host
                    ? new SkillHostArtifactManifest(
                        artifact.Host,
                        metadataPath,
                        metadataDigest,
                        artifact.MaterializedFrontmatterDigest)
                    : artifact)
                .ToArray());
        var manifest = SkillTestData.WithComputedManifestDigest(manifestCandidate);
        var manifestText = new SkillManifestJsonSerializer().Serialize(manifest);
        var files = package.Files
            .Where(file => file.RelativePath != package.Manifest.HostArtifacts.Single(artifact => artifact.Host == host).Path)
            .Select(file => string.Equals(file.RelativePath.Value, "agent-skill.json", StringComparison.Ordinal)
                ? new PackageTextFile(file.RelativePath, manifestText)
                : file)
            .Append(new PackageTextFile(metadataPath, metadataContent))
            .ToArray();
        return SkillTestData.CreateCanonicalPackage(manifest, files);
    }

}
