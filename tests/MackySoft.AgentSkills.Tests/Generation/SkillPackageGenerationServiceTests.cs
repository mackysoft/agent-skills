using System.Globalization;
using MackySoft.AgentSkills.Catalogs;
using MackySoft.AgentSkills.Digests;
using MackySoft.AgentSkills.Generation;
using MackySoft.AgentSkills.Hosts.Claude;
using MackySoft.AgentSkills.Hosts.Contracts;
using MackySoft.AgentSkills.Hosts.Copilot;
using MackySoft.AgentSkills.Hosts.OpenAi;
using MackySoft.AgentSkills.Hosts.Registration;
using MackySoft.AgentSkills.Manifests;
using MackySoft.AgentSkills.Names;
using MackySoft.AgentSkills.Packaging.Canonical;
using MackySoft.AgentSkills.Shared;
using MackySoft.AgentSkills.Sources;
using MackySoft.AgentSkills.Tiers;
using MackySoft.Tests;

namespace MackySoft.AgentSkills.Tests.Generation;

public sealed class SkillPackageGenerationServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task GenerateAllAsync_GeneratesCanonicalPackagesWithValidManifests ()
    {
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var validator = SkillTestData.CreateManifestValidator();

        Assert.Equal(SkillTestData.ExpectedSkillNames, packages.Select(static package => package.Manifest.SkillName.Value).ToArray());
        foreach (var package in packages)
        {
            Assert.Contains(package.Files, static file => string.Equals(file.RelativePath, "SKILL.md", StringComparison.Ordinal));
            Assert.Contains(package.Files, static file => string.Equals(file.RelativePath, "agent-skill.json", StringComparison.Ordinal));
            Assert.Contains(package.Files, static file => string.Equals(file.RelativePath, "agents/openai.yaml", StringComparison.Ordinal));
            Assert.True(validator.Validate(package.Manifest).IsSuccess);
            Assert.Equal(SkillManifest.CurrentSchemaVersion, package.Manifest.SchemaVersion);
            Assert.Equal(1, package.Manifest.SkillBundleVersion);
            Assert.False(string.IsNullOrWhiteSpace(package.Manifest.DisplayName));
            Assert.False(string.IsNullOrWhiteSpace(package.Manifest.Description));
            Assert.Empty(package.Manifest.Dependencies);
            Assert.Equal("basic", package.Manifest.Tier.Value);
            Assert.Equal(
                new[] { ClaudeSkillHostAdapter.HostKey, CopilotSkillHostAdapter.HostKey, OpenAiSkillHostAdapter.HostKey },
                package.Manifest.HostArtifacts.Select(static artifact => artifact.Host).ToArray());
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GenerateAllAsync_ComputesContentDigestFromBodyAndReferencesOnly ()
    {
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var calculator = new SkillDigestCalculator();

        foreach (var package in packages)
        {
            var expectedDigest = calculator.ComputeDigest(package.Files
                .Where(static file => string.Equals(file.RelativePath, "SKILL.md", StringComparison.Ordinal)
                    || file.RelativePath.StartsWith("references/", StringComparison.Ordinal))
                .Select(static file => new SkillDigestInputFile(file.RelativePath, file.Content)));

            Assert.Equal(expectedDigest, package.Manifest.ContentDigest);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GenerateAllAsync_ComputesManifestDigestFromCanonicalManifestJsonExcludingManifestDigest ()
    {
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var serializer = new SkillManifestJsonSerializer();
        var calculator = new SkillManifestDigestCalculator(serializer);

        foreach (var package in packages)
        {
            var expectedDigest = calculator.ComputeManifestDigest(package.Manifest);
            var selfDriftedManifest = package.Manifest with
            {
                ManifestDigest = new string('f', 64),
            };

            Assert.Equal(expectedDigest, package.Manifest.ManifestDigest);
            Assert.Equal(expectedDigest, calculator.ComputeManifestDigest(selfDriftedManifest));
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GenerateAllAsync_GeneratesByteIdenticalManifestJsonFromIdenticalInputs ()
    {
        var first = await SkillTestData.GenerateFixturePackagesAsync();
        var second = await SkillTestData.GenerateFixturePackagesAsync();

        Assert.Equal(
            first.Select(static package => GetManifestContent(package)).ToArray(),
            second.Select(static package => GetManifestContent(package)).ToArray());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GeneratedManifestJson_RoundTrips ()
    {
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var serializer = new SkillManifestJsonSerializer();

        foreach (var package in packages)
        {
            var manifestFile = package.Files.Single(static file => string.Equals(file.RelativePath, "agent-skill.json", StringComparison.Ordinal));
            var manifest = serializer.Deserialize(manifestFile.Content);

            Assert.Equal(SkillManifest.CurrentSchemaVersion, package.Manifest.SchemaVersion);
            Assert.Equal(SkillManifest.CurrentSchemaVersion, manifest.SchemaVersion);
            Assert.Equal(package.Manifest.SkillBundleVersion, manifest.SkillBundleVersion);
            Assert.Equal(package.Manifest.SkillName, manifest.SkillName);
            Assert.Equal(package.Manifest.DisplayName, manifest.DisplayName);
            Assert.Equal(package.Manifest.Description, manifest.Description);
            Assert.Equal(package.Manifest.Dependencies, manifest.Dependencies);
            Assert.Equal(package.Manifest.ContentDigest, manifest.ContentDigest);
            Assert.Equal(package.Manifest.ManifestDigest, manifest.ManifestDigest);
            Assert.Equal(package.Manifest.HostArtifacts, manifest.HostArtifacts);
            Assert.Equal(manifestFile.Content, serializer.Serialize(manifest));
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Generate_UsesOrdinalFileOrdering_ForCultureSensitiveReferencesAndHostArtifacts ()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            var manifestSerializer = new SkillManifestJsonSerializer();
            var service = new SkillPackageGenerationService(
                new SkillSourceDefinitionReader(),
                new SkillHostAdapterSet(
                [
                    new TestSkillHostAdapter("host-b", "agents/B.yaml"),
                    new TestSkillHostAdapter("host-a", "agents/a.yaml"),
                ]),
                new SkillDigestCalculator(),
                manifestSerializer,
                new SkillManifestDigestCalculator(manifestSerializer));
            var package = service.Generate(new SkillSourceDefinition(
                new SkillSourceMetadata(
                    SkillSourceMetadata.CurrentSchemaVersion,
                    1,
                    new SkillCatalogId("com.mackysoft.agent-skills"),
                    new SkillTier("basic"),
                    new SkillName("ordinal-culture-contract"),
                    "Ordinal Culture Contract",
                    "Use this skill to verify ordinal package ordering.",
                    [],
                    ["a.md", "B.md"]),
                "# Ordinal Culture Contract\n",
                [
                    new SkillSourceReference("a.md", "lowercase reference\n"),
                    new SkillSourceReference("B.md", "uppercase reference\n"),
                ]));

            var paths = package.Files.Select(static file => file.RelativePath).ToArray();
            var ordinalPaths = paths.Order(StringComparer.Ordinal).ToArray();

            Assert.Contains("agents/a.yaml", paths);
            Assert.Contains("agents/B.yaml", paths);
            Assert.Contains("references/a.md", paths);
            Assert.Contains("references/B.md", paths);
            Assert.Equal(ordinalPaths, paths);
            Assert.NotEqual(ordinalPaths, paths.Order(StringComparer.CurrentCulture).ToArray());
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Generate_SupportsDefinitionsWithoutReferences ()
    {
        var service = SkillTestData.CreatePackageGenerationService();
        var package = service.Generate(new SkillSourceDefinition(
            new SkillSourceMetadata(
                SkillSourceMetadata.CurrentSchemaVersion,
                1,
                new SkillCatalogId("com.mackysoft.agent-skills"),
                new SkillTier("basic"),
                new SkillName("reference-free-skill"),
                "Reference Free Skill",
                "Use this skill to verify reference-free package generation.",
                [],
                []),
            "# Reference Free Skill\n",
            []));

        var paths = package.Files.Select(static file => file.RelativePath).ToArray();

        Assert.Contains("SKILL.md", paths);
        Assert.Contains("agent-skill.json", paths);
        Assert.Contains("agents/openai.yaml", paths);
        Assert.DoesNotContain(paths, static path => path.StartsWith("references/", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Generate_PrependsTopLevelHeadingToTemplateBody ()
    {
        var service = SkillTestData.CreatePackageGenerationService();
        var package = service.Generate(new SkillSourceDefinition(
            new SkillSourceMetadata(
                SkillSourceMetadata.CurrentSchemaVersion,
                1,
                new SkillCatalogId("com.mackysoft.agent-skills"),
                new SkillTier("basic"),
                new SkillName("heading-free-skill"),
                "Heading Free Skill",
                "Use this skill to verify generated heading insertion.",
                [],
                []),
            "Use this skill to verify generated heading insertion.\n",
            []));

        var body = package.Files.Single(static file => string.Equals(file.RelativePath, "SKILL.md", StringComparison.Ordinal)).Content;

        Assert.StartsWith("# heading-free-skill\n\nUse this skill", body, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GenerateAllAsync_RejectsEmptyDefinitionsRoot ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "empty-definitions");
        var service = SkillTestData.CreatePackageGenerationService();

        var result = await service.GenerateAllAsync(scope.CreateDirectory("SkillDefinitions"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.SourceInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GenerateAllAsync_DetectsDependencyReferencesFromDescriptionBodyAndReferencesAsync ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "dependency-reference-sources");
        WriteDefinition(scope, "target-description");
        WriteDefinition(scope, "target-body");
        WriteDefinition(scope, "target-reference");
        WriteDefinition(
            scope,
            "source-skill",
            dependencies: ["target-reference", "target-body", "target-description"],
            description: "Use when invoking $target-description before other helpers.",
            skillTemplate: "Use $target-body for body work.\n",
            references: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["workflow.md"] = "Use $target-reference for reference work.\n",
            });
        var service = SkillTestData.CreatePackageGenerationService();

        var result = await service.GenerateAllAsync(scope.FullPath, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var source = result.Value!.Single(static package => package.Manifest.SkillName.Value == "source-skill");
        Assert.Equal(["target-body", "target-description", "target-reference"], source.Manifest.Dependencies.Select(static dependency => dependency.Value).ToArray());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GenerateAllAsync_RejectsDependencyMissingFromSourceTextAsync ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "dependency-missing-source-reference");
        WriteDefinition(scope, "target-skill");
        WriteDefinition(scope, "source-skill", dependencies: ["target-skill"]);
        var service = SkillTestData.CreatePackageGenerationService();

        var result = await service.GenerateAllAsync(scope.FullPath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.SourceInvalid, result.Failure!.Code);
        Assert.Contains("not referenced", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GenerateAllAsync_RejectsUndeclaredKnownSkillReferenceAsync ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "dependency-undeclared-source-reference");
        WriteDefinition(scope, "target-skill");
        WriteDefinition(scope, "source-skill", skillTemplate: "Use $target-skill.\n");
        var service = SkillTestData.CreatePackageGenerationService();

        var result = await service.GenerateAllAsync(scope.FullPath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.SourceInvalid, result.Failure!.Code);
        Assert.Contains("undeclared", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GenerateAllAsync_IgnoresUnknownSkillReferenceWhenMatchingDependenciesAsync ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "dependency-unknown-source-reference");
        WriteDefinition(scope, "source-skill", skillTemplate: "Use $unknown-skill when it is available elsewhere.\n");
        var service = SkillTestData.CreatePackageGenerationService();

        var result = await service.GenerateAllAsync(scope.FullPath, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Empty(result.Value!.Single().Manifest.Dependencies);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GenerateAllAsync_IgnoresSelfReferenceWhenMatchingDependenciesAsync ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "dependency-self-reference");
        WriteDefinition(scope, "source-skill", skillTemplate: "Use $source-skill to restart the workflow.\n");
        var service = SkillTestData.CreatePackageGenerationService();

        var result = await service.GenerateAllAsync(scope.FullPath, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Empty(result.Value!.Single().Manifest.Dependencies);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GenerateAllAsync_RejectsMixedSkillBundleVersions ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "mixed-skill-bundle-version");
        var definitionsRoot = scope.CreateDirectory("SkillDefinitions");
        WriteDefinition(scope, "SkillDefinitions/skill-a", skillBundleVersion: 1);
        WriteDefinition(scope, "SkillDefinitions/skill-b", skillBundleVersion: 2);
        var service = SkillTestData.CreatePackageGenerationService();

        var result = await service.GenerateAllAsync(definitionsRoot, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.SourceInvalid, result.Failure!.Code);
        Assert.Contains("skillBundleVersion", result.Failure.Message, StringComparison.Ordinal);
    }

    private sealed class TestSkillHostAdapter : ISkillHostAdapter
    {
        public TestSkillHostAdapter (
            string hostKey,
            string metadataArtifactPath)
        {
            MetadataArtifactPath = metadataArtifactPath;
            Descriptor = new SkillHostDescriptor(
                HostKey: hostKey,
                SupportsProjectScope: true,
                SupportsUserScope: true,
                ProjectDefaultTargetPath: $".{hostKey}/skills",
                UserDefaultTargetPath: "~/.test/skills",
                UserTargetRootPolicy: new SkillUserTargetRootPolicy(null, null, ".test/skills"),
                RequiresMetadataArtifact: true,
                MetadataArtifactPath: metadataArtifactPath,
                ReloadGuidance: "Reload test skills.");
        }

        public SkillHostDescriptor Descriptor { get; }

        public string MetadataArtifactPath { get; }

        public SkillHostArtifactSet BuildArtifacts (SkillHostMetadata metadata)
        {
            ArgumentNullException.ThrowIfNull(metadata);
            return new SkillHostArtifactSet(
                $"---\nname: \"{metadata.SkillName}\"\ndescription: \"{metadata.Description}\"\n---\n",
                $"host: \"{Descriptor.HostKey}\"\nskill: \"{metadata.SkillName}\"\n");
        }
    }

    private static string GetManifestContent (CanonicalSkillPackage package)
    {
        return package.Files.Single(static file => string.Equals(file.RelativePath, "agent-skill.json", StringComparison.Ordinal)).Content;
    }

    private static string WriteDefinition (
        TestDirectoryScope scope,
        string relativeDirectory,
        int skillBundleVersion = 1,
        IReadOnlyList<string>? dependencies = null,
        string? description = null,
        string? skillTemplate = null,
        IReadOnlyDictionary<string, string>? references = null)
    {
        dependencies ??= [];
        references ??= new Dictionary<string, string>(StringComparer.Ordinal);
        var skillName = Path.GetFileName(relativeDirectory);
        var dependencyJson = dependencies.Count == 0
            ? "[]"
            : "[\n" + string.Join(",\n", dependencies.Select(static dependency => $"    \"{dependency}\"")) + "\n  ]";
        var referenceJson = references.Count == 0
            ? "[]"
            : "[\n" + string.Join(",\n", references.Keys.Order(StringComparer.Ordinal).Select(static reference => $"    \"{reference}\"")) + "\n  ]";
        var skillDirectory = scope.CreateDirectory(relativeDirectory);
        scope.WriteFile(
            Path.Combine(relativeDirectory, "skill.json"),
            $$"""
            {
              "schemaVersion": 1,
              "skillBundleVersion": {{skillBundleVersion}},
              "catalogId": "com.mackysoft.agent-skills",
              "tier": "basic",
              "skillName": "{{skillName}}",
              "displayName": "{{skillName}}",
              "description": "{{description ?? "Use when testing dependency package generation."}}",
              "dependencies": {{dependencyJson}},
              "references": {{referenceJson}}
            }
            """);
        scope.WriteFile(Path.Combine(relativeDirectory, "SKILL.md.template"), skillTemplate ?? $"Use {skillName} when testing dependency package generation.\n");
        foreach (var reference in references.OrderBy(static item => item.Key, StringComparer.Ordinal))
        {
            scope.WriteFile(Path.Combine(relativeDirectory, "references", reference.Key + ".template"), reference.Value);
        }

        return skillDirectory;
    }
}
