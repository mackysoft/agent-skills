using System.Text;
using MackySoft.AgentSkills.Digests;
using MackySoft.AgentSkills.Manifests;
using MackySoft.AgentSkills.Shared;
using MackySoft.Tests;

namespace MackySoft.AgentSkills.Tests.Packaging.Canonical;

public sealed class CanonicalSkillPackageReaderTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAllAsync_ReadsGeneratedSkillsMatchingSourceGeneration ()
    {
        var sourcePackages = await SkillTestData.GenerateFixturePackagesAsync();
        var reader = SkillTestData.CreatePackageReader();

        var generatedPackages = await reader.ReadAllAsync(SkillTestData.GetGeneratedSkillsRoot(), CancellationToken.None);

        Assert.True(generatedPackages.IsSuccess, generatedPackages.Failure?.Message);
        var actualPackages = generatedPackages.Value!;
        Assert.Equal(SkillTestData.ExpectedSkillNames, actualPackages.Select(static package => package.Manifest.SkillName.Value).ToArray());
        Assert.Equal(
            sourcePackages.SelectMany(static package => package.Files.Select(file => $"{package.Manifest.SkillName.Value}/{file.RelativePath}={file.Content}")).Order(StringComparer.Ordinal).ToArray(),
            actualPackages.SelectMany(static package => package.Files.Select(file => $"{package.Manifest.SkillName.Value}/{file.RelativePath}={file.Content}")).Order(StringComparer.Ordinal).ToArray());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAllAsync_RejectsContentDigestDrift ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "generated-content-drift");
        var skillsRoot = CopyGeneratedSkills(scope);
        await File.AppendAllTextAsync(Path.Combine(skillsRoot, SkillTestData.ExpectedSkillNames[0], "SKILL.md"), "\nDrifted body.\n");
        var reader = SkillTestData.CreatePackageReader();

        var result = await reader.ReadAllAsync(skillsRoot, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.ManifestInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAllAsync_RejectsHostArtifactDigestDrift ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "generated-host-artifact-drift");
        var skillsRoot = CopyGeneratedSkills(scope);
        await File.AppendAllTextAsync(Path.Combine(skillsRoot, SkillTestData.ExpectedSkillNames[0], "agents", "openai.yaml"), "\n# drift\n");
        var reader = SkillTestData.CreatePackageReader();

        var result = await reader.ReadAllAsync(skillsRoot, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.ManifestInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAllAsync_RejectsManifestDigestDrift ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "generated-manifest-drift");
        var skillsRoot = CopyGeneratedSkills(scope);
        var manifestPath = Path.Combine(skillsRoot, SkillTestData.ExpectedSkillNames[0], "agent-skill.json");
        var serializer = new SkillManifestJsonSerializer();
        var manifest = serializer.Deserialize(await File.ReadAllTextAsync(manifestPath));
        var driftedManifest = manifest with
        {
            DisplayName = manifest.DisplayName + " Drifted",
        };
        await File.WriteAllTextAsync(manifestPath, serializer.Serialize(driftedManifest));
        var reader = SkillTestData.CreatePackageReader();

        var result = await reader.ReadAllAsync(skillsRoot, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.ManifestInvalid, result.Failure!.Code);
        Assert.Contains("manifestDigest", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAllAsync_RejectsTamperedManifestDigest ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "generated-manifest-digest-tampered");
        var skillsRoot = CopyGeneratedSkills(scope);
        var manifestPath = Path.Combine(skillsRoot, SkillTestData.ExpectedSkillNames[0], "agent-skill.json");
        var serializer = new SkillManifestJsonSerializer();
        var manifest = serializer.Deserialize(await File.ReadAllTextAsync(manifestPath));
        var driftedManifest = manifest with
        {
            ManifestDigest = new string('f', 64),
        };
        await File.WriteAllTextAsync(manifestPath, serializer.Serialize(driftedManifest));
        var reader = SkillTestData.CreatePackageReader();

        var result = await reader.ReadAllAsync(skillsRoot, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.ManifestInvalid, result.Failure!.Code);
        Assert.Contains("manifestDigest", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAllAsync_RejectsManifestCrLfLineEndings ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "generated-manifest-crlf");
        var skillsRoot = CopyGeneratedSkills(scope);
        var manifestPath = Path.Combine(skillsRoot, SkillTestData.ExpectedSkillNames[0], "agent-skill.json");
        var manifestText = await File.ReadAllTextAsync(manifestPath);
        await File.WriteAllTextAsync(manifestPath, manifestText.Replace("\n", "\r\n", StringComparison.Ordinal));
        var reader = SkillTestData.CreatePackageReader();

        var result = await reader.ReadAllAsync(skillsRoot, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.ManifestInvalid, result.Failure!.Code);
        Assert.Contains("not canonical", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAllAsync_RejectsManifestUtf8ByteOrderMark ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "generated-manifest-bom");
        var skillsRoot = CopyGeneratedSkills(scope);
        var manifestPath = Path.Combine(skillsRoot, SkillTestData.ExpectedSkillNames[0], "agent-skill.json");
        var manifestText = await File.ReadAllTextAsync(manifestPath);
        await File.WriteAllBytesAsync(manifestPath, [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(manifestText)]);
        var reader = SkillTestData.CreatePackageReader();

        var result = await reader.ReadAllAsync(skillsRoot, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.ManifestInvalid, result.Failure!.Code);
        Assert.Contains("byte order mark", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAllAsync_RejectsHostArtifactAdapterOutputDrift ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "generated-host-artifact-adapter-drift");
        var skillsRoot = CopyGeneratedSkills(scope);
        var skillDirectory = Path.Combine(skillsRoot, SkillTestData.ExpectedSkillNames[0]);
        var artifactPath = Path.Combine(skillDirectory, "agents", "openai.yaml");
        var driftedArtifact = "interface:\n  display_name: Drifted\n  short_description: Drifted\n  default_prompt: Drifted\n\npolicy:\n  allow_implicit_invocation: false\n";
        await File.WriteAllTextAsync(artifactPath, driftedArtifact);

        var manifestPath = Path.Combine(skillDirectory, "agent-skill.json");
        var serializer = new SkillManifestJsonSerializer();
        var manifest = serializer.Deserialize(await File.ReadAllTextAsync(manifestPath));
        var driftedDigest = new SkillDigestCalculator().ComputeSingleFileDigest("agents/openai.yaml", driftedArtifact);
        var driftedManifest = manifest with
        {
            HostArtifacts = manifest.HostArtifacts
                .Select(artifact => artifact.Host == "openai"
                    ? artifact with { Digest = driftedDigest }
                    : artifact)
                .ToArray(),
        };
        driftedManifest = WithComputedManifestDigest(driftedManifest);
        await File.WriteAllTextAsync(manifestPath, serializer.Serialize(driftedManifest));
        var reader = SkillTestData.CreatePackageReader();

        var result = await reader.ReadAllAsync(skillsRoot, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.ManifestInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAllAsync_RejectsFrontmatterDigestDrift ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "generated-frontmatter-drift");
        var skillsRoot = CopyGeneratedSkills(scope);
        var manifestPath = Path.Combine(skillsRoot, SkillTestData.ExpectedSkillNames[0], "agent-skill.json");
        var serializer = new SkillManifestJsonSerializer();
        var manifest = serializer.Deserialize(await File.ReadAllTextAsync(manifestPath));
        var driftedManifest = manifest with
        {
            HostArtifacts = manifest.HostArtifacts
                .Select(static artifact => artifact.Host == "claude"
                    ? artifact with { MaterializedFrontmatterDigest = new string('0', 64) }
                    : artifact)
                .ToArray(),
        };
        driftedManifest = WithComputedManifestDigest(driftedManifest);
        await File.WriteAllTextAsync(manifestPath, serializer.Serialize(driftedManifest));
        var reader = SkillTestData.CreatePackageReader();

        var result = await reader.ReadAllAsync(skillsRoot, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.ManifestInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAllAsync_RejectsUnsafePackageRelativePathWithoutThrowing ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "generated-unsafe-path");
        var skillsRoot = CopyGeneratedSkills(scope);
        var unsafePath = Path.Combine(skillsRoot, SkillTestData.ExpectedSkillNames[0], "bad\\name.md");
        await File.WriteAllTextAsync(unsafePath, "unsafe path");
        var reader = SkillTestData.CreatePackageReader();

        var result = await reader.ReadAllAsync(skillsRoot, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.ManifestInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAllAsync_RejectsPackageFileSymlinkBeforeReading ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "generated-file-symlink");
        var skillsRoot = CopyGeneratedSkills(scope);
        var outsideFile = scope.WriteFile("outside.md", "outside content");
        var linkPath = Path.Combine(skillsRoot, SkillTestData.ExpectedSkillNames[0], "references", "linked.md");
        File.CreateSymbolicLink(linkPath, outsideFile);
        var reader = SkillTestData.CreatePackageReader();

        var result = await reader.ReadAllAsync(skillsRoot, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.ManifestInvalid, result.Failure!.Code);
        Assert.Contains("non-regular file", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAllAsync_PreservesPathUnsafeFromPackageDirectoryBoundary ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "generated-directory-symlink");
        var skillsRoot = scope.CreateDirectory("skills");
        var outsideDirectory = scope.CreateDirectory("outside");
        Directory.CreateSymbolicLink(Path.Combine(skillsRoot, "linked-skill"), outsideDirectory);
        var reader = SkillTestData.CreatePackageReader();

        var result = await reader.ReadAllAsync(skillsRoot, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.PathUnsafe, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAllAsync_RejectsNestedPackageDirectorySymlinkBeforeRecursing ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "generated-nested-directory-symlink");
        var skillsRoot = CopyGeneratedSkills(scope);
        var outsideDirectory = scope.CreateDirectory("outside");
        var linkPath = Path.Combine(skillsRoot, SkillTestData.ExpectedSkillNames[0], "references", "linked-directory");
        Directory.CreateSymbolicLink(linkPath, outsideDirectory);
        var reader = SkillTestData.CreatePackageReader();

        var result = await reader.ReadAllAsync(skillsRoot, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.ManifestInvalid, result.Failure!.Code);
        Assert.Contains("non-regular directory", result.Failure.Message, StringComparison.Ordinal);
    }

    private static string CopyGeneratedSkills (TestDirectoryScope scope)
    {
        var targetRoot = scope.CreateDirectory("skills");
        CopyDirectory(SkillTestData.GetGeneratedSkillsRoot(), targetRoot);
        return targetRoot;
    }

    private static void CopyDirectory (
        string sourceDirectory,
        string targetDirectory)
    {
        foreach (var directoryPath in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(targetDirectory, Path.GetRelativePath(sourceDirectory, directoryPath)));
        }

        foreach (var filePath in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var targetPath = Path.Combine(targetDirectory, Path.GetRelativePath(sourceDirectory, filePath));
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            File.Copy(filePath, targetPath, overwrite: true);
        }
    }

    private static SkillManifest WithComputedManifestDigest (SkillManifest manifest)
    {
        var serializer = new SkillManifestJsonSerializer();
        return new SkillManifestDigestCalculator(serializer)
            .WithComputedManifestDigest(manifest);
    }
}
