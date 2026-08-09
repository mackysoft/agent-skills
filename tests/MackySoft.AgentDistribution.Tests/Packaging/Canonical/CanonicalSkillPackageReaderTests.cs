using System.Text;
using MackySoft.AgentDistribution.Digests;
using MackySoft.AgentDistribution.Manifests;
using MackySoft.AgentDistribution.Shared;

namespace MackySoft.AgentDistribution.Tests.Packaging.Canonical;

public sealed class CanonicalSkillPackageReaderTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAllAsync_ReadsGeneratedSkillsMatchingSourceGeneration ()
    {
        var sourcePackages = await SkillTestData.GenerateFixturePackagesAsync();
        var reader = SkillTestData.CreatePackageReader();

        var generatedPackages = await reader.ReadAllAsync(AbsolutePath.Parse(SkillTestData.GetGeneratedSkillsRoot()), CancellationToken.None);

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
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "generated-content-drift");
        var skillsRoot = CopyGeneratedSkills(scope);
        await File.AppendAllTextAsync(Path.Combine(skillsRoot.Value, SkillTestData.ExpectedSkillNames[0], "SKILL.md"), "\nDrifted body.\n");
        var reader = SkillTestData.CreatePackageReader();

        var result = await reader.ReadAllAsync(skillsRoot, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.ManifestInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAllAsync_RejectsHostArtifactDigestDrift ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "generated-host-artifact-drift");
        var skillsRoot = CopyGeneratedSkills(scope);
        await File.AppendAllTextAsync(Path.Combine(skillsRoot.Value, SkillTestData.ExpectedSkillNames[0], "agents", "openai.yaml"), "\n# drift\n");
        var reader = SkillTestData.CreatePackageReader();

        var result = await reader.ReadAllAsync(skillsRoot, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.ManifestInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAllAsync_RejectsManifestDigestDrift ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "generated-manifest-drift");
        var skillsRoot = CopyGeneratedSkills(scope);
        var manifestPath = Path.Combine(skillsRoot.Value, SkillTestData.ExpectedSkillNames[0], "agent-skill.json");
        var serializer = new SkillManifestJsonSerializer();
        var manifestText = await File.ReadAllTextAsync(manifestPath);
        var manifest = serializer.Deserialize(manifestText);
        var driftedManifestText = manifestText.Replace(
            manifest.DisplayName,
            manifest.DisplayName + " Drifted",
            StringComparison.Ordinal);
        await File.WriteAllTextAsync(manifestPath, driftedManifestText);
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
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "generated-manifest-digest-tampered");
        var skillsRoot = CopyGeneratedSkills(scope);
        var manifestPath = Path.Combine(skillsRoot.Value, SkillTestData.ExpectedSkillNames[0], "agent-skill.json");
        var serializer = new SkillManifestJsonSerializer();
        var manifestText = await File.ReadAllTextAsync(manifestPath);
        var manifest = serializer.Deserialize(manifestText);
        var driftedDigest = string.Equals(manifest.ManifestDigest!.ToString(), new string('f', 64), StringComparison.Ordinal)
            ? new string('0', 64)
            : new string('f', 64);
        await File.WriteAllTextAsync(
            manifestPath,
            manifestText.Replace(manifest.ManifestDigest.ToString(), driftedDigest, StringComparison.Ordinal));
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
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "generated-manifest-crlf");
        var skillsRoot = CopyGeneratedSkills(scope);
        var manifestPath = Path.Combine(skillsRoot.Value, SkillTestData.ExpectedSkillNames[0], "agent-skill.json");
        var manifestText = await File.ReadAllTextAsync(manifestPath);
        await File.WriteAllTextAsync(manifestPath, manifestText.Replace("\n", "\r\n", StringComparison.Ordinal));
        var reader = SkillTestData.CreatePackageReader();

        var result = await reader.ReadAllAsync(skillsRoot, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.ManifestInvalid, result.Failure!.Code);
        Assert.Contains("LF line endings", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAllAsync_RejectsManifestUtf8ByteOrderMark ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "generated-manifest-bom");
        var skillsRoot = CopyGeneratedSkills(scope);
        var manifestPath = Path.Combine(skillsRoot.Value, SkillTestData.ExpectedSkillNames[0], "agent-skill.json");
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
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "generated-host-artifact-adapter-drift");
        var skillsRoot = CopyGeneratedSkills(scope);
        var skillDirectory = Path.Combine(skillsRoot.Value, SkillTestData.ExpectedSkillNames[0]);
        var artifactPath = Path.Combine(skillDirectory, "agents", "openai.yaml");
        var driftedArtifact = "interface:\n  display_name: Drifted\n  short_description: Drifted\n  default_prompt: Drifted\n\npolicy:\n  allow_implicit_invocation: false\n";
        await File.WriteAllTextAsync(artifactPath, driftedArtifact);

        var manifestPath = Path.Combine(skillDirectory, "agent-skill.json");
        var serializer = new SkillManifestJsonSerializer();
        var manifest = serializer.Deserialize(await File.ReadAllTextAsync(manifestPath));
        var driftedDigest = new SkillDigestCalculator().ComputeSingleFileDigest(PackageRelativePath.Parse("agents/openai.yaml"), driftedArtifact);
        var driftedManifest = SkillTestData.CopyManifest(
            manifest,
            hostArtifacts: manifest.HostArtifacts
                .Select(artifact => artifact.Host == HostKind.Codex
                    ? new SkillHostArtifactManifest(artifact.Host, artifact.Path, driftedDigest, artifact.MaterializedFrontmatterDigest)
                    : artifact)
                .ToArray());
        var canonicalDriftedManifest = SkillTestData.WithComputedManifestDigest(driftedManifest);
        await File.WriteAllTextAsync(manifestPath, serializer.Serialize(canonicalDriftedManifest));
        var reader = SkillTestData.CreatePackageReader();

        var result = await reader.ReadAllAsync(skillsRoot, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.ManifestInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAllAsync_RejectsFrontmatterDigestDrift ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "generated-frontmatter-drift");
        var skillsRoot = CopyGeneratedSkills(scope);
        var manifestPath = Path.Combine(skillsRoot.Value, SkillTestData.ExpectedSkillNames[0], "agent-skill.json");
        var serializer = new SkillManifestJsonSerializer();
        var manifest = serializer.Deserialize(await File.ReadAllTextAsync(manifestPath));
        var driftedManifest = SkillTestData.CopyManifest(
            manifest,
            hostArtifacts: manifest.HostArtifacts
                .Select(static artifact => artifact.Host == HostKind.ClaudeCode
                    ? new SkillHostArtifactManifest(artifact.Host, artifact.Path, artifact.Digest, Sha256Digest.Parse(new string('0', 64)))
                    : artifact)
                .ToArray());
        var canonicalDriftedManifest = SkillTestData.WithComputedManifestDigest(driftedManifest);
        await File.WriteAllTextAsync(manifestPath, serializer.Serialize(canonicalDriftedManifest));
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

        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "generated-unsafe-path");
        var skillsRoot = CopyGeneratedSkills(scope);
        var unsafePath = Path.Combine(skillsRoot.Value, SkillTestData.ExpectedSkillNames[0], "bad\\name.md");
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

        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "generated-file-symlink");
        var skillsRoot = CopyGeneratedSkills(scope);
        var outsideFile = scope.WriteFile("outside.md", "outside content");
        var linkPath = Path.Combine(skillsRoot.Value, SkillTestData.ExpectedSkillNames[0], "references", "linked.md");
        File.CreateSymbolicLink(linkPath, outsideFile);
        var reader = SkillTestData.CreatePackageReader();

        var result = await reader.ReadAllAsync(skillsRoot, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.ManifestInvalid, result.Failure!.Code);
        Assert.Contains("non-regular path", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAllAsync_PreservesPathUnsafeFromPackageDirectoryBoundary ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "generated-directory-symlink");
        var skillsRoot = AbsolutePath.Parse(scope.CreateDirectory("skills"));
        var outsideDirectory = scope.CreateDirectory("outside");
        Directory.CreateSymbolicLink(Path.Combine(skillsRoot.Value, "linked-skill"), outsideDirectory);
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

        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "generated-nested-directory-symlink");
        var skillsRoot = CopyGeneratedSkills(scope);
        var outsideDirectory = scope.CreateDirectory("outside");
        var linkPath = Path.Combine(skillsRoot.Value, SkillTestData.ExpectedSkillNames[0], "references", "linked-directory");
        Directory.CreateSymbolicLink(linkPath, outsideDirectory);
        var reader = SkillTestData.CreatePackageReader();

        var result = await reader.ReadAllAsync(skillsRoot, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.ManifestInvalid, result.Failure!.Code);
        Assert.Contains("non-regular path", result.Failure.Message, StringComparison.Ordinal);
    }

    private static AbsolutePath CopyGeneratedSkills (TestDirectoryScope scope)
    {
        var targetRoot = AbsolutePath.Parse(scope.CreateDirectory("skills"));
        CopyDirectory(SkillTestData.GetGeneratedSkillsRoot(), targetRoot.Value);
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

}
