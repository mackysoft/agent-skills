using MackySoft.AgentSkills.Bundles;
using MackySoft.AgentSkills.Digests;
using MackySoft.AgentSkills.Manifests;
using MackySoft.AgentSkills.Shared;
using MackySoft.Tests;

namespace MackySoft.AgentSkills.Tests.Bundles;

public sealed class CanonicalSkillBundleReaderWriterTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task WriteAndRead_RoundTripsDescriptorAndCompletePackageSet ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "canonical-bundle-roundtrip");
        var bundle = await SkillTestData.GenerateFixtureBundleAsync();
        var outputRoot = scope.GetPath("generated");
        var oldMarker = scope.WriteFile("generated/old-bundle.txt", "old bundle\n");
        var services = CreateServices();

        var writeResult = await services.Writer.WriteAsync(bundle, outputRoot, CancellationToken.None);
        var readResult = await services.Reader.ReadAsync(outputRoot, CancellationToken.None);

        Assert.True(writeResult.IsSuccess, writeResult.Failure?.Message);
        Assert.True(readResult.IsSuccess, readResult.Failure?.Message);
        Assert.Equal(bundle.Descriptor.SchemaVersion, readResult.Value!.Descriptor.SchemaVersion);
        Assert.Equal(bundle.Descriptor.CatalogId, readResult.Value.Descriptor.CatalogId);
        Assert.Equal(bundle.Descriptor.SkillBundleVersion, readResult.Value.Descriptor.SkillBundleVersion);
        Assert.Equal(bundle.Descriptor.BundleDigest, readResult.Value.Descriptor.BundleDigest);
        Assert.Equal(
            bundle.Packages.Select(static package => package.Manifest.SkillName),
            readResult.Value.Packages.Select(static package => package.Manifest.SkillName));
        Assert.True(File.Exists(Path.Combine(outputRoot, "bundle.json")));
        Assert.False(File.Exists(oldMarker));
        Assert.Empty(GetPublicationArtifacts(scope.FullPath));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAsync_RejectsDescriptorDigestThatDoesNotMatchPackageSet ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "canonical-bundle-digest-mismatch");
        var bundle = await SkillTestData.GenerateFixtureBundleAsync();
        var outputRoot = scope.GetPath("generated");
        var services = CreateServices();
        var writeResult = await services.Writer.WriteAsync(bundle, outputRoot, CancellationToken.None);
        Assert.True(writeResult.IsSuccess, writeResult.Failure?.Message);
        var fDigest = Sha256Digest.Parse(new string('f', 64));
        var replacementDigest = bundle.Descriptor.BundleDigest == fDigest
            ? Sha256Digest.Parse(new string('e', 64))
            : fDigest;
        var driftedDescriptor = new SkillBundleDescriptor(
            bundle.Descriptor.SchemaVersion,
            bundle.Descriptor.CatalogId,
            bundle.Descriptor.SkillBundleVersion,
            replacementDigest);
        File.WriteAllText(
            Path.Combine(outputRoot, "bundle.json"),
            services.BundleSerializer.SerializeDescriptor(driftedDescriptor));

        var readResult = await services.Reader.ReadAsync(outputRoot, CancellationToken.None);

        Assert.False(readResult.IsSuccess);
        Assert.Equal(SkillFailureCodes.ManifestInvalid, readResult.Failure!.Code);
        Assert.Contains("bundleDigest", readResult.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAsync_RejectsPackageDirectorySymbolicLink ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "canonical-bundle-package-symlink");
        var bundle = await SkillTestData.GenerateFixtureBundleAsync();
        var outputRoot = scope.GetPath("generated");
        var services = CreateServices();
        var writeResult = await services.Writer.WriteAsync(bundle, outputRoot, CancellationToken.None);
        Assert.True(writeResult.IsSuccess, writeResult.Failure?.Message);

        var packageDirectory = Path.Combine(outputRoot, bundle.Packages[0].Manifest.SkillName.Value);
        if (!TestSymbolicLinks.TryCreateDirectory(Path.Combine(outputRoot, "linked-package"), packageDirectory))
        {
            return;
        }

        var readResult = await services.Reader.ReadAsync(outputRoot, CancellationToken.None);

        Assert.False(readResult.IsSuccess);
        Assert.Equal(SkillFailureCodes.PathUnsafe, readResult.Failure!.Code);
        Assert.Contains("non-regular package directory", readResult.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WriteAsync_CancellationPreservesExistingBundle ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "canonical-bundle-cancelled");
        var bundle = await SkillTestData.GenerateFixtureBundleAsync();
        var outputRoot = scope.CreateDirectory("generated");
        var oldMarker = scope.WriteFile("generated/old-bundle.txt", "old bundle\n");
        var services = CreateServices();
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await services.Writer.WriteAsync(bundle, outputRoot, cancellationSource.Token));

        Assert.True(File.Exists(oldMarker));
        Assert.False(File.Exists(Path.Combine(outputRoot, "bundle.json")));
        Assert.Empty(GetPublicationArtifacts(scope.FullPath));
    }

    private static BundleServices CreateServices ()
    {
        var bundleSerializer = new SkillBundleJsonSerializer();
        var bundleDigestCalculator = new SkillBundleDigestCalculator(new SkillManifestJsonSerializer());
        var bundleFactory = new CanonicalSkillBundle.Factory(bundleDigestCalculator);
        var bundleReader = new CanonicalSkillBundleReader(
            SkillTestData.CreatePackageReader(),
            bundleSerializer,
            bundleFactory);
        return new BundleServices(
            bundleSerializer,
            new CanonicalSkillBundleWriter(
                SkillTestData.CreateCanonicalPackageWriter(),
                bundleSerializer,
                bundleReader),
            bundleReader);
    }

    private sealed record BundleServices (
        SkillBundleJsonSerializer BundleSerializer,
        CanonicalSkillBundleWriter Writer,
        CanonicalSkillBundleReader Reader);

    private static IReadOnlyList<string> GetPublicationArtifacts (string parentDirectory)
    {
        return Directory.EnumerateFileSystemEntries(parentDirectory)
            .Where(static path => Path.GetFileName(path).StartsWith(".generated.", StringComparison.Ordinal))
            .ToArray();
    }
}
