using MackySoft.AgentDistribution.Bundles;
using MackySoft.AgentDistribution.Shared;
using MackySoft.Tests;

namespace MackySoft.AgentDistribution.Tests.Bundles;

public sealed class SourceAndGeneratedBundleTransactionTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task PublishAsync_WhenSourcePublicationFails_RestoresPreviousGeneratedBundle ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-bundles", "transaction-source-failure");
        scope.WriteFile("bundle.json", "original source\n");
        scope.WriteFile("generated/bundle.json", "original generated\n");
        var transaction = new SourceAndGeneratedBundleTransaction(
            static (_, _, _) => ValueTask.FromException(new IOException("Injected source publication failure.")));

        await Assert.ThrowsAsync<IOException>(async () =>
            await transaction.PublishAsync(
                AbsolutePath.Parse(scope.FullPath),
                "updated source\n",
                PublishGeneratedAsync,
                CancellationToken.None));

        Assert.Equal("original source\n", File.ReadAllText(scope.GetPath("bundle.json")));
        Assert.Equal("original generated\n", File.ReadAllText(scope.GetPath("generated/bundle.json")));
        AssertNoBackup(scope);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task PublishAsync_WhenSourcePublicationFailsWithoutExistingGeneratedBundle_RemovesGeneratedBundle ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-bundles", "transaction-new-source-failure");
        scope.WriteFile("bundle.json", "original source\n");
        var transaction = new SourceAndGeneratedBundleTransaction(
            static (_, _, _) => ValueTask.FromException(new IOException("Injected source publication failure.")));

        await Assert.ThrowsAsync<IOException>(async () =>
            await transaction.PublishAsync(
                AbsolutePath.Parse(scope.FullPath),
                "updated source\n",
                PublishGeneratedAsync,
                CancellationToken.None));

        Assert.Equal("original source\n", File.ReadAllText(scope.GetPath("bundle.json")));
        Assert.False(Directory.Exists(scope.GetPath("generated")));
        AssertNoBackup(scope);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task PublishAsync_WhenSourcePublicationIsCancelled_RestoresPreviousGeneratedBundle ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-bundles", "transaction-source-cancel");
        scope.WriteFile("bundle.json", "original source\n");
        scope.WriteFile("generated/bundle.json", "original generated\n");
        using var cancellationSource = new CancellationTokenSource();
        var sourcePublicationAttempted = false;
        var transaction = new SourceAndGeneratedBundleTransaction((_, _, cancellationToken) =>
        {
            sourcePublicationAttempted = true;
            cancellationSource.Cancel();
            return ValueTask.FromCanceled(cancellationToken);
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await transaction.PublishAsync(
                AbsolutePath.Parse(scope.FullPath),
                "updated source\n",
                PublishGeneratedAsync,
                cancellationSource.Token));

        Assert.True(sourcePublicationAttempted);
        Assert.Equal("original source\n", File.ReadAllText(scope.GetPath("bundle.json")));
        Assert.Equal("original generated\n", File.ReadAllText(scope.GetPath("generated/bundle.json")));
        AssertNoBackup(scope);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task PublishAsync_WhenGeneratedOutputIsSymbolicLink_ReturnsPathUnsafeWithoutPublishing ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-bundles", "transaction-generated-link");
        using var outside = TestDirectories.CreateTempScope("agent-distribution-bundles", "transaction-generated-link-outside");
        scope.WriteFile("bundle.json", "original source\n");
        try
        {
            Directory.CreateSymbolicLink(scope.GetPath("generated"), outside.FullPath);
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }
        catch (PlatformNotSupportedException)
        {
            return;
        }

        var generatedPublicationAttempted = false;
        var transaction = new SourceAndGeneratedBundleTransaction(
            static (_, _, _) => ValueTask.CompletedTask);
        var result = await transaction.PublishAsync(
            AbsolutePath.Parse(scope.FullPath),
            "updated source\n",
            (outputRoot, cancellationToken) =>
            {
                generatedPublicationAttempted = true;
                return PublishGeneratedAsync(outputRoot, cancellationToken);
            },
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.PathUnsafe, result.Failure!.Code);
        Assert.False(generatedPublicationAttempted);
        Assert.Equal("original source\n", File.ReadAllText(scope.GetPath("bundle.json")));
    }

    private static ValueTask<SkillOperationResult<AbsolutePath>> PublishGeneratedAsync (
        AbsolutePath outputRoot,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(outputRoot.Value);
        File.WriteAllText(Path.Combine(outputRoot.Value, "bundle.json"), "updated generated\n");
        return ValueTask.FromResult(SkillOperationResult<AbsolutePath>.Success(outputRoot));
    }

    private static void AssertNoBackup (TestDirectoryScope scope)
    {
        Assert.DoesNotContain(
            Directory.EnumerateFileSystemEntries(scope.FullPath),
            static path => Path.GetFileName(path).StartsWith(".generated.build-backup.", StringComparison.Ordinal));
    }
}
