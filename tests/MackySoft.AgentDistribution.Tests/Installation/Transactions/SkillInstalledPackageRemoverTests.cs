using MackySoft.AgentDistribution.Installation.Contracts;
using MackySoft.AgentDistribution.Installation.Transactions;
using MackySoft.AgentDistribution.Shared;

namespace MackySoft.AgentDistribution.Tests.Installation.Transactions;

public sealed class SkillInstalledPackageRemoverTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task DeleteAsync_WhenMovedDirectoryCleanupFails_CommitsDeletionWithoutRestoringPartialTree ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "remover-delete-failure-restores");
        var targetRoot = scope.CreateDirectory(".agents/skills");
        var skillDirectory = scope.CreateDirectory(Path.Combine(".agents", "skills", "sample-skill"));
        scope.WriteFile(Path.Combine(".agents", "skills", "sample-skill", "SKILL.md"), "# Existing\n");
        scope.WriteFile(Path.Combine(".agents", "skills", "sample-skill", "nested", "file.md"), "# Nested\n");
        var remover = new SkillInstalledPackageRemover(new DeleteMovedDirectoryFailingOperations());

        var result = await remover.DeleteAsync(
            AbsolutePath.Parse(targetRoot),
            AbsolutePath.Parse(skillDirectory),
            null,
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.False(Directory.Exists(skillDirectory));
        var transactionRoot = Path.Combine(targetRoot, ".agent-distribution-skill-transactions");
        Assert.True(Directory.Exists(transactionRoot));
        Assert.Single(Directory.EnumerateDirectories(transactionRoot));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task DeleteAsync_WhenMovedTargetPreconditionFails_RestoresTargetAndCleansWorkspace ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "remover-moved-precondition-failure");
        var targetRoot = scope.CreateDirectory(".agents/skills");
        var skillDirectory = scope.CreateDirectory(Path.Combine(".agents", "skills", "sample-skill"));
        var skillPath = scope.WriteFile(Path.Combine(".agents", "skills", "sample-skill", "SKILL.md"), "# Existing\n");
        var remover = SkillTestData.CreatePackageRemover();
        var preconditionCallCount = 0;

        var result = await remover.DeleteAsync(
            AbsolutePath.Parse(targetRoot),
            AbsolutePath.Parse(skillDirectory),
            (_, _) => ValueTask.FromResult(
                ++preconditionCallCount == 1
                    ? AgentDistributionOperationResult<bool>.Success(true)
                    : AgentDistributionOperationResult<bool>.FailureResult(AgentDistributionFailureCodes.InstallTargetDigestMismatch, "Synthetic moved target failure.")),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.InstallTargetDigestMismatch, result.Failure!.Code);
        Assert.True(Directory.Exists(skillDirectory));
        Assert.Equal("# Existing\n", File.ReadAllText(skillPath));
        AssertSharedLockRootHasNoWorkspaces(targetRoot);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task DeleteAsync_WithPreconditionWhenTargetIsMissing_ReturnsChangedTargetFailure ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "remover-missing-precondition");
        var targetRoot = scope.CreateDirectory(".agents/skills");
        var skillDirectory = Path.Combine(targetRoot, "sample-skill");
        var remover = SkillTestData.CreatePackageRemover();
        var preconditionCallCount = 0;

        var result = await remover.DeleteAsync(
            AbsolutePath.Parse(targetRoot),
            AbsolutePath.Parse(skillDirectory),
            (_, _) =>
            {
                preconditionCallCount++;
                return ValueTask.FromResult(AgentDistributionOperationResult<bool>.Success(true));
            },
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.InstallTargetDigestMismatch, result.Failure!.Code);
        Assert.Equal(1, preconditionCallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task DeleteAsync_WhenCancellationOccursAfterBackupMove_RestoresTargetBeforePropagatingCancellation ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "remover-cancellation-restores");
        using var cancellationSource = new CancellationTokenSource();
        var targetRoot = scope.CreateDirectory(".agents/skills");
        var skillDirectory = scope.CreateDirectory(Path.Combine(".agents", "skills", "sample-skill"));
        var skillPath = scope.WriteFile(Path.Combine(".agents", "skills", "sample-skill", "SKILL.md"), "# Existing\n");
        var remover = SkillTestData.CreatePackageRemover();
        var preconditionCallCount = 0;

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await remover.DeleteAsync(
                AbsolutePath.Parse(targetRoot),
                AbsolutePath.Parse(skillDirectory),
                (_, cancellationToken) =>
                {
                    if (++preconditionCallCount == 2)
                    {
                        cancellationSource.Cancel();
                        cancellationToken.ThrowIfCancellationRequested();
                    }

                    return ValueTask.FromResult(AgentDistributionOperationResult<bool>.Success(true));
                },
                cancellationSource.Token));

        Assert.True(Directory.Exists(skillDirectory));
        Assert.Equal("# Existing\n", File.ReadAllText(skillPath));
        AssertSharedLockRootHasNoWorkspaces(targetRoot);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task DeleteAsync_WhenMovedTargetPreconditionCancelsWithoutThrowing_RestoresTargetBeforeDeletion ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "remover-cancellation-after-precondition");
        using var cancellationSource = new CancellationTokenSource();
        var targetRoot = scope.CreateDirectory(".agents/skills");
        var skillDirectory = scope.CreateDirectory(Path.Combine(".agents", "skills", "sample-skill"));
        var skillPath = scope.WriteFile(Path.Combine(".agents", "skills", "sample-skill", "SKILL.md"), "# Existing\n");
        var remover = SkillTestData.CreatePackageRemover();
        var preconditionCallCount = 0;

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await remover.DeleteAsync(
                AbsolutePath.Parse(targetRoot),
                AbsolutePath.Parse(skillDirectory),
                (_, _) =>
                {
                    if (++preconditionCallCount == 2)
                    {
                        cancellationSource.Cancel();
                    }

                    return ValueTask.FromResult(AgentDistributionOperationResult<bool>.Success(true));
                },
                cancellationSource.Token));

        Assert.True(Directory.Exists(skillDirectory));
        Assert.Equal("# Existing\n", File.ReadAllText(skillPath));
        AssertSharedLockRootHasNoWorkspaces(targetRoot);
    }

    private static void AssertSharedLockRootHasNoWorkspaces (string targetRoot)
    {
        var transactionLockRoot = Path.Combine(targetRoot, ".agent-distribution-skill-transactions");

        Assert.True(Directory.Exists(transactionLockRoot));
        Assert.Empty(Directory.EnumerateDirectories(transactionLockRoot));
    }

    private sealed class DeleteMovedDirectoryFailingOperations : ISkillPackageDirectoryOperations
    {
        public bool Exists (AbsolutePath path)
        {
            return Directory.Exists(path.Value);
        }

        public void Create (AbsolutePath path)
        {
            Directory.CreateDirectory(path.Value);
        }

        public void Move (
            AbsolutePath sourceDirectoryName,
            AbsolutePath destinationDirectoryName)
        {
            Directory.Move(sourceDirectoryName.Value, destinationDirectoryName.Value);
        }

        public void Delete (
            AbsolutePath path,
            bool recursive)
        {
            if (path.Value.EndsWith("sample-skill", StringComparison.Ordinal))
            {
                File.Delete(Path.Combine(path.Value, "SKILL.md"));
                throw new IOException("Injected moved directory delete failure.");
            }

            Directory.Delete(path.Value, recursive);
        }
    }
}
