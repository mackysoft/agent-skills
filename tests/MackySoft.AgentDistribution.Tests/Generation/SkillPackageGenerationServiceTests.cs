using System.Globalization;
using MackySoft.AgentDistribution.Bundles;
using MackySoft.AgentDistribution.Catalogs;
using MackySoft.AgentDistribution.Digests;
using MackySoft.AgentDistribution.Manifests;
using MackySoft.AgentDistribution.Packaging.Canonical;
using MackySoft.AgentDistribution.Shared;
using MackySoft.AgentDistribution.Sources;

namespace MackySoft.AgentDistribution.Tests.Generation;

public sealed class SkillPackageGenerationServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task GenerateAllAsync_GeneratesCanonicalPackagesWithValidManifests ()
    {
        var packages = await SkillTestData.GenerateFixturePackagesAsync();

        Assert.Equal(SkillTestData.ExpectedSkillNames, packages.Select(static package => package.Manifest.SkillName.Value).ToArray());
        foreach (var package in packages)
        {
            Assert.Contains(package.Files, static file => string.Equals(file.RelativePath.Value, "SKILL.md", StringComparison.Ordinal));
            Assert.Contains(package.Files, static file => string.Equals(file.RelativePath.Value, "agent-skill.json", StringComparison.Ordinal));
            Assert.Contains(package.Files, static file => string.Equals(file.RelativePath.Value, "agents/openai.yaml", StringComparison.Ordinal));
            Assert.Equal(SkillManifest.CurrentSchemaVersion, package.Manifest.SchemaVersion);
            Assert.Equal(1, package.Manifest.SkillBundleVersion.Value);
            Assert.False(string.IsNullOrWhiteSpace(package.Manifest.DisplayName));
            Assert.False(string.IsNullOrWhiteSpace(package.Manifest.Description));
            Assert.Empty(package.Manifest.Dependencies);
            Assert.Equal(SkillTestData.ExpectedCategory, package.Manifest.Category.Value);
            Assert.Equal(
                new[] { HostKind.Codex, HostKind.ClaudeCode, HostKind.GitHubCopilot },
                package.Manifest.HostArtifacts.Select(static artifact => artifact.Host).ToArray());
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GenerateAllAsync_ComputesContentDigestFromBodyReferencesAndScripts ()
    {
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var calculator = new PackageContentDigestCalculator();

        foreach (var package in packages)
        {
            var expectedDigest = calculator.ComputeDigest(package.Files
                .Where(static file => string.Equals(file.RelativePath.Value, "SKILL.md", StringComparison.Ordinal)
                    || file.RelativePath.Value.StartsWith("references/", StringComparison.Ordinal)
                    || file.RelativePath.Value.StartsWith("scripts/", StringComparison.Ordinal))
                .Select(static file => new PackageContentDigestInputFile(file.RelativePath, file.Content)));

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
            var selfDriftedManifest = SkillTestData.CopyManifest(
                package.Manifest,
                manifestDigest: Sha256Digest.Parse(new string('f', 64)));

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
    public async Task GenerateAllAsync_GeneratesNestedScriptsAndDetectsScriptContentSetChanges ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "generated-scripts");
        WriteDefinition(
            scope,
            "script-skill",
            scripts: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["bench/collect.sh"] = "#!/bin/sh\necho collect\n",
                ["report.py"] = "print('report')\n",
            });
        var service = SkillTestData.CreatePackageGenerationService();

        var initialResult = await service.GenerateAllAsync(AbsolutePath.Parse(scope.FullPath), CancellationToken.None);

        Assert.True(initialResult.IsSuccess, initialResult.Failure?.Message);
        var initial = Assert.Single(initialResult.Value!.Packages);
        Assert.Equal(
            ["scripts/bench/collect.sh", "scripts/report.py"],
            initial.Files
                .Where(static file => file.RelativePath.Value.StartsWith("scripts/", StringComparison.Ordinal))
                .Select(static file => file.RelativePath.Value)
                .ToArray());
        Assert.Equal(
            new PackageContentDigestCalculator().ComputeDigest(initial.Files
                .Where(static file => file.RelativePath.Value == "SKILL.md"
                    || file.RelativePath.Value.StartsWith("references/", StringComparison.Ordinal)
                    || file.RelativePath.Value.StartsWith("scripts/", StringComparison.Ordinal))
                .Select(static file => new PackageContentDigestInputFile(file.RelativePath, file.Content))),
            initial.Manifest.ContentDigest);

        scope.WriteFile("definitions/core/script-skill/scripts/bench/collect.sh", "#!/bin/sh\necho changed\n");
        var changedResult = await service.GenerateAllAsync(AbsolutePath.Parse(scope.FullPath), CancellationToken.None);

        Assert.True(changedResult.IsSuccess, changedResult.Failure?.Message);
        Assert.NotEqual(initial.Manifest.ContentDigest, changedResult.Value!.Packages[0].Manifest.ContentDigest);
        Assert.NotEqual(initialResult.Value.Descriptor.BundleDigest, changedResult.Value.Descriptor.BundleDigest);

        File.Delete(scope.GetPath("definitions/core/script-skill/scripts/report.py"));
        var removedResult = await service.GenerateAllAsync(AbsolutePath.Parse(scope.FullPath), CancellationToken.None);

        Assert.True(removedResult.IsSuccess, removedResult.Failure?.Message);
        Assert.NotEqual(changedResult.Value.Packages[0].Manifest.ContentDigest, removedResult.Value!.Packages[0].Manifest.ContentDigest);
        Assert.NotEqual(changedResult.Value.Descriptor.BundleDigest, removedResult.Value.Descriptor.BundleDigest);
        Assert.DoesNotContain(
            removedResult.Value.Packages[0].Files,
            static file => string.Equals(file.RelativePath.Value, "scripts/report.py", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GeneratedManifestJson_RoundTrips ()
    {
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var serializer = new SkillManifestJsonSerializer();

        foreach (var package in packages)
        {
            var manifestFile = package.Files.Single(static file => string.Equals(file.RelativePath.Value, "agent-skill.json", StringComparison.Ordinal));
            var candidate = serializer.Deserialize(manifestFile.Content);
            var result = SkillTestData.CreateManifestFactory(serializer).CreateCanonical(candidate);
            Assert.True(result.IsSuccess, result.Failure?.Message);
            var manifest = result.Value!;

            Assert.Equal(SkillManifest.CurrentSchemaVersion, package.Manifest.SchemaVersion);
            Assert.Equal(SkillManifest.CurrentSchemaVersion, manifest.SchemaVersion);
            Assert.Equal(package.Manifest.SkillBundleVersion, manifest.SkillBundleVersion);
            Assert.Equal(package.Manifest.Category, manifest.Category);
            Assert.Equal(package.Manifest.SkillName, manifest.SkillName);
            Assert.Equal(package.Manifest.DisplayName, manifest.DisplayName);
            Assert.Equal(package.Manifest.Description, manifest.Description);
            Assert.Equal(package.Manifest.Dependencies, manifest.Dependencies);
            Assert.Equal(package.Manifest.ContentDigest, manifest.ContentDigest);
            Assert.Equal(package.Manifest.ManifestDigest, manifest.ManifestDigest);
            Assert.Equal(
                package.Manifest.HostArtifacts.Select(ToHostArtifactContract).ToArray(),
                manifest.HostArtifacts.Select(ToHostArtifactContract).ToArray());
            Assert.Equal(manifestFile.Content, serializer.Serialize(manifest));
        }
    }

    private static (
        HostKind Host,
        string? Path,
        Sha256Digest? Digest,
        Sha256Digest MaterializedFrontmatterDigest) ToHostArtifactContract (SkillHostArtifactManifest artifact)
    {
        return (
            artifact.Host,
            artifact.Path?.Value,
            artifact.Digest,
            artifact.MaterializedFrontmatterDigest);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Generate_UsesOrdinalFileOrdering_ForCultureSensitiveReferences ()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            var service = SkillTestData.CreatePackageGenerationService();
            var package = service.Generate(CreateBundleDefinition(), new SkillSourceDefinition(
                new SkillSourceMetadata(
                    SkillSourceMetadata.CurrentSchemaVersion,
                    new SkillCategory(SkillTestData.ExpectedCategory),
                    new SkillName("ordinal-culture-contract"),
                    "Ordinal Culture Contract",
                    "Use this skill to verify ordinal package ordering.",
                    [],
                    ["a.md", "B.md"]),
                "Use this skill to verify ordinal package ordering.\n",
                [
                    new SkillSourceReference("a.md", "lowercase reference\n"),
                    new SkillSourceReference("B.md", "uppercase reference\n"),
                ],
                []));

            var paths = package.Files.Select(static file => file.RelativePath.Value).ToArray();
            var ordinalPaths = paths.Order(StringComparer.Ordinal).ToArray();

            Assert.Contains("agents/openai.yaml", paths);
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
        var package = service.Generate(CreateBundleDefinition(), new SkillSourceDefinition(
            new SkillSourceMetadata(
                SkillSourceMetadata.CurrentSchemaVersion,
                new SkillCategory(SkillTestData.ExpectedCategory),
                new SkillName("reference-free-skill"),
                "Reference Free Skill",
                "Use this skill to verify reference-free package generation.",
                [],
                []),
            "Use this skill to verify reference-free package generation.\n",
            [],
            []));

        var paths = package.Files.Select(static file => file.RelativePath.Value).ToArray();

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
        var package = service.Generate(CreateBundleDefinition(), new SkillSourceDefinition(
            new SkillSourceMetadata(
                SkillSourceMetadata.CurrentSchemaVersion,
                new SkillCategory(SkillTestData.ExpectedCategory),
                new SkillName("heading-free-skill"),
                "Heading Free Skill",
                "Use this skill to verify generated heading insertion.",
                [],
                []),
            "Use this skill to verify generated heading insertion.\n",
            [],
            []));

        var body = package.Files.Single(static file => string.Equals(file.RelativePath.Value, "SKILL.md", StringComparison.Ordinal)).Content;

        Assert.StartsWith("# heading-free-skill\n\nUse this skill", body, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GenerateAllAsync_RejectsEmptyDefinitionsRoot ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "empty-definitions");
        var service = SkillTestData.CreatePackageGenerationService();
        if (!File.Exists(scope.GetPath("bundle.json")))
        {
            WriteBundle(scope);
        }
        scope.CreateDirectory("definitions");

        var result = await service.GenerateAllAsync(AbsolutePath.Parse(scope.FullPath), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.SourceInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GenerateAllAsync_RejectsDefinitionsDirectorySymlinkOutsideBundleRoot ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "definitions-symlink");
        using var outsideScope = TestDirectories.CreateTempScope("agent-distribution-skills", "definitions-symlink-outside");
        WriteBundle(scope);
        WriteDefinition(outsideScope, "outside-skill");
        Directory.CreateSymbolicLink(scope.GetPath("definitions"), outsideScope.GetPath("definitions"));

        var service = SkillTestData.CreatePackageGenerationService();

        var result = await service.GenerateAllAsync(AbsolutePath.Parse(scope.FullPath), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.SourceInvalid, result.Failure!.Code);
        Assert.Contains("definitions", result.Failure.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GenerateAllAsync_DetectsDependencyReferencesFromBodyAndReferencesAsync ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "dependency-reference-sources");
        WriteDefinition(scope, "target-body");
        WriteDefinition(scope, "target-reference");
        WriteDefinition(
            scope,
            "source-skill",
            dependencies: ["target-reference", "target-body"],
            skillTemplate: "Use $target-body for body work.\n",
            references: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["workflow.md"] = "Use $target-reference for reference work.\n",
            });
        var service = SkillTestData.CreatePackageGenerationService();

        var result = await service.GenerateAllAsync(AbsolutePath.Parse(scope.FullPath), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var source = result.Value!.Packages.Single(static package => package.Manifest.SkillName.Value == "source-skill");
        Assert.Equal(["target-body", "target-reference"], source.Manifest.Dependencies.Select(static dependency => dependency.Value).ToArray());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GenerateAllAsync_IgnoresDependencyReferencesFromDescriptionAsync ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "dependency-description-reference");
        WriteDefinition(scope, "target-skill");
        WriteDefinition(
            scope,
            "source-skill",
            description: "Use when invoking $target-skill is mentioned as metadata.",
            skillTemplate: "Use this skill without another skill.\n");
        var service = SkillTestData.CreatePackageGenerationService();

        var result = await service.GenerateAllAsync(AbsolutePath.Parse(scope.FullPath), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var source = result.Value!.Packages.Single(static package => package.Manifest.SkillName.Value == "source-skill");
        Assert.Empty(source.Manifest.Dependencies);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GenerateAllAsync_RejectsDependencyMissingFromSourceTextAsync ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "dependency-missing-source-reference");
        WriteDefinition(scope, "target-skill");
        WriteDefinition(scope, "source-skill", dependencies: ["target-skill"]);
        var service = SkillTestData.CreatePackageGenerationService();

        var result = await service.GenerateAllAsync(AbsolutePath.Parse(scope.FullPath), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.SourceInvalid, result.Failure!.Code);
        Assert.Contains("not referenced", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GenerateAllAsync_RejectsUndeclaredKnownSkillReferenceAsync ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "dependency-undeclared-source-reference");
        WriteDefinition(scope, "target-skill");
        WriteDefinition(scope, "source-skill", skillTemplate: "Use $target-skill.\n");
        var service = SkillTestData.CreatePackageGenerationService();

        var result = await service.GenerateAllAsync(AbsolutePath.Parse(scope.FullPath), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.SourceInvalid, result.Failure!.Code);
        Assert.Contains("undeclared", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GenerateAllAsync_IgnoresUnknownSkillReferenceWhenMatchingDependenciesAsync ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "dependency-unknown-source-reference");
        WriteDefinition(scope, "source-skill", skillTemplate: "Use $unknown-skill when it is available elsewhere.\n");
        var service = SkillTestData.CreatePackageGenerationService();

        var result = await service.GenerateAllAsync(AbsolutePath.Parse(scope.FullPath), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Empty(result.Value!.Packages.Single().Manifest.Dependencies);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GenerateAllAsync_IgnoresSelfReferenceWhenMatchingDependenciesAsync ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "dependency-self-reference");
        WriteDefinition(scope, "source-skill", skillTemplate: "Use $source-skill to restart the workflow.\n");
        var service = SkillTestData.CreatePackageGenerationService();

        var result = await service.GenerateAllAsync(AbsolutePath.Parse(scope.FullPath), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Empty(result.Value!.Packages.Single().Manifest.Dependencies);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GenerateAllAsync_StampsAuthoredBundleIdentityIntoDescriptorAndPackages ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "authored-bundle-identity");
        WriteBundle(scope, skillBundleVersion: 7);
        WriteDefinition(scope, "skill-a");
        WriteDefinition(scope, "skill-b");
        var service = SkillTestData.CreatePackageGenerationService();

        var result = await service.GenerateAllAsync(AbsolutePath.Parse(scope.FullPath), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal("com.mackysoft.agent-distribution", result.Value!.Descriptor.CatalogId.Value);
        Assert.Equal(7, result.Value.Descriptor.SkillBundleVersion.Value);
        Assert.All(result.Value.Packages, static package =>
        {
            Assert.Equal("com.mackysoft.agent-distribution", package.Manifest.CatalogId.Value);
            Assert.Equal(7, package.Manifest.SkillBundleVersion.Value);
        });
    }

    private static string GetManifestContent (CanonicalSkillPackage package)
    {
        return package.Files.Single(static file => string.Equals(file.RelativePath.Value, "agent-skill.json", StringComparison.Ordinal)).Content;
    }

    private static string WriteDefinition (
        TestDirectoryScope scope,
        string relativeDirectory,
        IReadOnlyList<string>? dependencies = null,
        string? description = null,
        string? skillTemplate = null,
        IReadOnlyDictionary<string, string>? references = null,
        IReadOnlyDictionary<string, string>? scripts = null)
    {
        dependencies ??= [];
        references ??= new Dictionary<string, string>(StringComparer.Ordinal);
        scripts ??= new Dictionary<string, string>(StringComparer.Ordinal);
        if (!File.Exists(scope.GetPath("bundle.json")))
        {
            WriteBundle(scope);
        }

        var skillName = Path.GetFileName(relativeDirectory);
        var sourceRelativeDirectory = Path.Combine("definitions", SkillTestData.ExpectedCategory, relativeDirectory);
        var dependencyJson = dependencies.Count == 0
            ? "[]"
            : "[\n" + string.Join(",\n", dependencies.Select(static dependency => $"    \"{dependency}\"")) + "\n  ]";
        var skillDirectory = scope.CreateDirectory(sourceRelativeDirectory);
        scope.WriteFile(
            Path.Combine(sourceRelativeDirectory, "skill.json"),
            $$"""
            {
              "schemaVersion": 1,
              "displayName": "{{skillName}}",
              "description": "{{description ?? "Use when testing dependency package generation."}}",
              "dependencies": {{dependencyJson}}
            }
            """);
        scope.WriteFile(Path.Combine(sourceRelativeDirectory, "SKILL.md.template"), skillTemplate ?? $"Use {skillName} when testing dependency package generation.\n");
        foreach (var reference in references.OrderBy(static item => item.Key, StringComparer.Ordinal))
        {
            scope.WriteFile(Path.Combine(sourceRelativeDirectory, "references", reference.Key + ".template"), reference.Value);
        }

        foreach (var script in scripts.OrderBy(static item => item.Key, StringComparer.Ordinal))
        {
            scope.WriteFile(Path.Combine(sourceRelativeDirectory, "scripts", script.Key), script.Value);
        }

        return skillDirectory;
    }

    private static SkillBundleDefinition CreateBundleDefinition (int skillBundleVersion = 1)
    {
        return new SkillBundleDefinition(
            SkillBundleDefinition.CurrentSchemaVersion,
            new AgentDistributionCatalogId("com.mackysoft.agent-distribution"),
            new SkillBundleVersion(skillBundleVersion));
    }

    private static void WriteBundle (
        TestDirectoryScope scope,
        int skillBundleVersion = 1)
    {
        var serializer = new SkillBundleJsonSerializer();
        scope.WriteFile("bundle.json", serializer.SerializeDefinition(CreateBundleDefinition(skillBundleVersion)));
    }

}
