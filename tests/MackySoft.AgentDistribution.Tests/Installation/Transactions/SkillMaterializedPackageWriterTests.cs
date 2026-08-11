using MackySoft.AgentDistribution.Installation.Contracts;
using MackySoft.AgentDistribution.Installation.Transactions;
using MackySoft.AgentDistribution.Materialization;
using MackySoft.AgentDistribution.Shared;

namespace MackySoft.AgentDistribution.Tests.Installation.Transactions;

public sealed class SkillMaterializedPackageWriterTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task WriteAsync_WhenPackagePathsConflict_ReturnsPathUnsafeAndPreservesExistingTarget ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "writer-staging-failure-preserves");
        var targetRoot = scope.CreateDirectory(".agents/skills");
        var skillDirectory = scope.CreateDirectory(Path.Combine(".agents", "skills", "sample-skill"));
        var skillPath = scope.WriteFile(Path.Combine(".agents", "skills", "sample-skill", "SKILL.md"), "# Existing\n");
        var writer = SkillTestData.CreatePackageWriter();
        var package = new SkillMaterializedPackage(
            new SkillName("sample-skill"),
            HostKind.Codex,
            [
                new PackageTextFile(PackageRelativePath.Parse("nested"), "file"),
                new PackageTextFile(PackageRelativePath.Parse("nested/file.md"), "nested"),
            ]);

        var result = await writer.WriteAsync(
            AbsolutePath.Parse(targetRoot),
            AbsolutePath.Parse(skillDirectory),
            package,
            SkillMaterializedPackageWriteMode.ReplaceExisting,
            null,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.PathUnsafe, result.Failure!.Code);
        Assert.True(Directory.Exists(skillDirectory));
        Assert.Equal("# Existing\n", File.ReadAllText(skillPath));
        Assert.False(Directory.Exists(Path.Combine(targetRoot, ".agent-distribution-skill-transactions")));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WriteAsync_WhenCommitMoveFails_RestoresExistingTargetAndCleansTransactionDirectory ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "writer-commit-failure-restores");
        var targetRoot = scope.CreateDirectory(".agents/skills");
        var skillDirectory = scope.CreateDirectory(Path.Combine(".agents", "skills", "sample-skill"));
        var skillPath = scope.WriteFile(Path.Combine(".agents", "skills", "sample-skill", "SKILL.md"), "# Existing\n");
        var writer = new SkillMaterializedPackageWriter(new CommitMoveFailingDirectoryOperations());
        var package = new SkillMaterializedPackage(
            new SkillName("sample-skill"),
            HostKind.Codex,
            [
                new PackageTextFile(PackageRelativePath.Parse("SKILL.md"), "# New\n"),
                new PackageTextFile(PackageRelativePath.Parse("new.md"), "# New file\n"),
            ]);

        var result = await writer.WriteAsync(
            AbsolutePath.Parse(targetRoot),
            AbsolutePath.Parse(skillDirectory),
            package,
            SkillMaterializedPackageWriteMode.ReplaceExisting,
            null,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.InstallTargetWriteFailed, result.Failure!.Code);
        Assert.True(Directory.Exists(skillDirectory));
        Assert.Equal("# Existing\n", File.ReadAllText(skillPath));
        Assert.False(File.Exists(Path.Combine(skillDirectory, "new.md")));
        Assert.False(Directory.Exists(Path.Combine(targetRoot, ".agent-distribution-skill-transactions")));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WriteAsync_WhenMovedTargetPreconditionFails_RestoresExistingTargetAndCleansTransactionDirectory ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "writer-moved-precondition-failure");
        var targetRoot = scope.CreateDirectory(".agents/skills");
        var skillDirectory = scope.CreateDirectory(Path.Combine(".agents", "skills", "sample-skill"));
        var skillPath = scope.WriteFile(Path.Combine(".agents", "skills", "sample-skill", "SKILL.md"), "# Existing\n");
        var writer = SkillTestData.CreatePackageWriter();
        var package = new SkillMaterializedPackage(
            new SkillName("sample-skill"),
            HostKind.Codex,
            [
                new PackageTextFile(PackageRelativePath.Parse("SKILL.md"), "# New\n"),
            ]);
        var preconditionCallCount = 0;

        var result = await writer.WriteAsync(
            AbsolutePath.Parse(targetRoot),
            AbsolutePath.Parse(skillDirectory),
            package,
            SkillMaterializedPackageWriteMode.ReplaceExisting,
            (_, _) => ValueTask.FromResult(
                ++preconditionCallCount == 1
                    ? AgentDistributionOperationResult<bool>.Success(true)
                    : AgentDistributionOperationResult<bool>.FailureResult(AgentDistributionFailureCodes.InstallTargetDigestMismatch, "Synthetic moved target failure.")),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.InstallTargetDigestMismatch, result.Failure!.Code);
        Assert.True(Directory.Exists(skillDirectory));
        Assert.Equal("# Existing\n", File.ReadAllText(skillPath));
        Assert.False(Directory.Exists(Path.Combine(targetRoot, ".agent-distribution-skill-transactions")));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WriteAsync_WithOutsideSkillDirectory_ReturnsPathUnsafeWithoutWriting ()
    {
        using var targetScope = TestDirectories.CreateTempScope("agent-distribution-skills", "writer-target-root");
        using var outsideScope = TestDirectories.CreateTempScope("agent-distribution-skills", "writer-outside-root");
        var writer = SkillTestData.CreatePackageWriter();
        var outsideSkillDirectory = Path.Combine(outsideScope.FullPath, "skill");

        var result = await writer.WriteAsync(
            AbsolutePath.Parse(targetScope.FullPath),
            AbsolutePath.Parse(outsideSkillDirectory),
            new SkillMaterializedPackage(new SkillName("skill"), HostKind.Codex, []),
            SkillMaterializedPackageWriteMode.CreateNew,
            null,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.PathUnsafe, result.Failure!.Code);
        Assert.False(Directory.Exists(outsideSkillDirectory));
        Assert.Empty(Directory.EnumerateFileSystemEntries(outsideScope.FullPath));
    }

    private sealed class CommitMoveFailingDirectoryOperations : ISkillPackageDirectoryOperations
    {
        private int moveCount;

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
            moveCount++;
            if (moveCount == 2)
            {
                throw new IOException("Injected commit move failure.");
            }

            Directory.Move(sourceDirectoryName.Value, destinationDirectoryName.Value);
        }

        public void Delete (
            AbsolutePath path,
            bool recursive)
        {
            Directory.Delete(path.Value, recursive);
        }
    }
}
