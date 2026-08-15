using MackySoft.AgentDistribution.Installation.Contracts;
using MackySoft.AgentDistribution.Installation.Transactions;
using MackySoft.AgentDistribution.Materialization;
using MackySoft.AgentDistribution.Shared;

namespace MackySoft.AgentDistribution.Tests.Installation.Transactions;

public sealed class SkillMaterializedPackageWriterTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void WriteRequest_WithUndefinedMode_RejectsBeforeWriting ()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SkillMaterializedPackageWriteRequest(
            AbsolutePath.Parse(Path.GetTempPath()),
            AbsolutePath.Parse(Path.Combine(Path.GetTempPath(), "skill")),
            new SkillMaterializedPackage(new SkillName("skill"), HostKind.Codex, []),
            (SkillMaterializedPackageWriteMode)99,
            null));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void WriteAsync_UsesRequestContract ()
    {
        var method = Assert.Single(typeof(ISkillMaterializedPackageWriter).GetMethods());

        Assert.Equal(nameof(ISkillMaterializedPackageWriter.WriteAsync), method.Name);
        Assert.Equal(
            [typeof(SkillMaterializedPackageWriteRequest), typeof(CancellationToken)],
            method.GetParameters().Select(static parameter => parameter.ParameterType));
    }

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
            CreateWriteRequest(targetRoot, skillDirectory, package, SkillMaterializedPackageWriteMode.ReplaceExisting, null),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.PathUnsafe, result.Failure!.Code);
        Assert.True(Directory.Exists(skillDirectory));
        Assert.Equal("# Existing\n", File.ReadAllText(skillPath));
        AssertSharedLockRootHasNoWorkspaces(targetRoot);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WriteAsync_WhenCommitMoveFails_RestoresExistingTargetAndCleansWorkspace ()
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
            CreateWriteRequest(targetRoot, skillDirectory, package, SkillMaterializedPackageWriteMode.ReplaceExisting, null),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.InstallTargetWriteFailed, result.Failure!.Code);
        Assert.True(Directory.Exists(skillDirectory));
        Assert.Equal("# Existing\n", File.ReadAllText(skillPath));
        Assert.False(File.Exists(Path.Combine(skillDirectory, "new.md")));
        AssertSharedLockRootHasNoWorkspaces(targetRoot);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WriteAsync_WhenMovedTargetPreconditionFails_RestoresExistingTargetAndCleansWorkspace ()
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
            CreateWriteRequest(targetRoot, skillDirectory, package, SkillMaterializedPackageWriteMode.ReplaceExisting, (_, _) => ValueTask.FromResult(++preconditionCallCount == 1
                ? AgentDistributionOperationResult<bool>.Success(true)
                : AgentDistributionOperationResult<bool>.FailureResult(AgentDistributionFailureCodes.InstallTargetDigestMismatch, "Synthetic moved target failure."))),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.InstallTargetDigestMismatch, result.Failure!.Code);
        Assert.True(Directory.Exists(skillDirectory));
        Assert.Equal("# Existing\n", File.ReadAllText(skillPath));
        AssertSharedLockRootHasNoWorkspaces(targetRoot);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WriteAsync_WhenSkillNameIsStaging_ReplacesExistingTarget ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "writer-staging-skill-name");
        var targetRoot = scope.CreateDirectory(".agents/skills");
        var skillDirectory = scope.CreateDirectory(Path.Combine(".agents", "skills", "staging"));
        scope.WriteFile(Path.Combine(".agents", "skills", "staging", "SKILL.md"), "# Existing\n");
        var writer = SkillTestData.CreatePackageWriter();

        var result = await writer.WriteAsync(
            CreateWriteRequest(
                targetRoot,
                skillDirectory,
                CreatePackage("staging"),
                SkillMaterializedPackageWriteMode.ReplaceExisting,
                null),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal("# New\n", File.ReadAllText(Path.Combine(skillDirectory, "SKILL.md")));
        AssertSharedLockRootHasNoWorkspaces(targetRoot);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WriteAsync_WhenCancellationOccursAfterBackupMove_RestoresTargetBeforePropagatingCancellation ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "writer-cancellation-restores");
        using var cancellationSource = new CancellationTokenSource();
        var targetRoot = scope.CreateDirectory(".agents/skills");
        var skillDirectory = scope.CreateDirectory(Path.Combine(".agents", "skills", "sample-skill"));
        var skillPath = scope.WriteFile(Path.Combine(".agents", "skills", "sample-skill", "SKILL.md"), "# Existing\n");
        var writer = SkillTestData.CreatePackageWriter();
        var preconditionCallCount = 0;

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await writer.WriteAsync(
                CreateWriteRequest(
                    targetRoot,
                    skillDirectory,
                    CreatePackage(),
                    SkillMaterializedPackageWriteMode.ReplaceExisting,
                    (_, cancellationToken) =>
                    {
                        if (++preconditionCallCount == 2)
                        {
                            cancellationSource.Cancel();
                            cancellationToken.ThrowIfCancellationRequested();
                        }

                        return ValueTask.FromResult(AgentDistributionOperationResult<bool>.Success(true));
                    }),
                cancellationSource.Token));

        Assert.True(Directory.Exists(skillDirectory));
        Assert.Equal("# Existing\n", File.ReadAllText(skillPath));
        AssertSharedLockRootHasNoWorkspaces(targetRoot);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WriteAsync_WhenMovedTargetPreconditionCancelsWithoutThrowing_RestoresTargetBeforeCommit ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "writer-cancellation-after-precondition");
        using var cancellationSource = new CancellationTokenSource();
        var targetRoot = scope.CreateDirectory(".agents/skills");
        var skillDirectory = scope.CreateDirectory(Path.Combine(".agents", "skills", "sample-skill"));
        var skillPath = scope.WriteFile(Path.Combine(".agents", "skills", "sample-skill", "SKILL.md"), "# Existing\n");
        var writer = SkillTestData.CreatePackageWriter();
        var preconditionCallCount = 0;

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await writer.WriteAsync(
                CreateWriteRequest(
                    targetRoot,
                    skillDirectory,
                    CreatePackage(),
                    SkillMaterializedPackageWriteMode.ReplaceExisting,
                    (_, _) =>
                    {
                        if (++preconditionCallCount == 2)
                        {
                            cancellationSource.Cancel();
                        }

                        return ValueTask.FromResult(AgentDistributionOperationResult<bool>.Success(true));
                    }),
                cancellationSource.Token));

        Assert.True(Directory.Exists(skillDirectory));
        Assert.Equal("# Existing\n", File.ReadAllText(skillPath));
        AssertSharedLockRootHasNoWorkspaces(targetRoot);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WriteAsync_WhenRestoreFails_PreservesBackupInItsWorkspace ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "writer-restore-failure-preserves-backup");
        var targetRoot = scope.CreateDirectory(".agents/skills");
        var skillDirectory = scope.CreateDirectory(Path.Combine(".agents", "skills", "sample-skill"));
        scope.WriteFile(Path.Combine(".agents", "skills", "sample-skill", "SKILL.md"), "# Existing\n");
        var writer = new SkillMaterializedPackageWriter(new CommitAndRestoreMoveFailingDirectoryOperations());

        var result = await writer.WriteAsync(
            CreateWriteRequest(targetRoot, skillDirectory, CreatePackage(), SkillMaterializedPackageWriteMode.ReplaceExisting, null),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.InstallTargetWriteFailed, result.Failure!.Code);
        Assert.False(Directory.Exists(skillDirectory));
        var workspace = Assert.Single(Directory.EnumerateDirectories(GetTransactionLockRoot(targetRoot)));
        Assert.Equal("# Existing\n", File.ReadAllText(Path.Combine(workspace, ".backup", "sample-skill", "SKILL.md")));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WriteAsync_WhenRemoverCannotAcquireSharedLock_PreservesWriterWorkspaceAndSharedLockRoot ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "writer-remover-shared-lock");
        var targetRoot = scope.CreateDirectory(".agents/skills");
        var skillDirectory = scope.CreateDirectory(Path.Combine(".agents", "skills", "sample-skill"));
        scope.WriteFile(Path.Combine(".agents", "skills", "sample-skill", "SKILL.md"), "# Existing\n");
        var writer = SkillTestData.CreatePackageWriter();
        var remover = SkillTestData.CreatePackageRemover();
        var writerHasLock = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseWriter = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var preconditionCallCount = 0;

        async ValueTask<AgentDistributionOperationResult<bool>> HoldWriterLockAsync (AbsolutePath _, CancellationToken cancellationToken)
        {
            if (++preconditionCallCount == 1)
            {
                writerHasLock.SetResult();
                await releaseWriter.Task.WaitAsync(cancellationToken);
            }

            return AgentDistributionOperationResult<bool>.Success(true);
        }

        var writerTask = writer.WriteAsync(
            CreateWriteRequest(targetRoot, skillDirectory, CreatePackage(), SkillMaterializedPackageWriteMode.ReplaceExisting, HoldWriterLockAsync),
            CancellationToken.None).AsTask();
        await writerHasLock.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var removalResult = await remover.DeleteAsync(
            AbsolutePath.Parse(targetRoot),
            AbsolutePath.Parse(skillDirectory),
            null,
            CancellationToken.None);
        releaseWriter.SetResult();
        var writerResult = await writerTask;

        Assert.False(removalResult.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.InstallTargetWriteFailed, removalResult.Failure!.Code);
        Assert.True(writerResult.IsSuccess, writerResult.Failure?.Message);
        AssertSharedLockRootHasNoWorkspaces(targetRoot);
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
            CreateWriteRequest(
                targetScope.FullPath,
                outsideSkillDirectory,
                new SkillMaterializedPackage(new SkillName("skill"), HostKind.Codex, []),
                SkillMaterializedPackageWriteMode.CreateNew,
                null),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.PathUnsafe, result.Failure!.Code);
        Assert.False(Directory.Exists(outsideSkillDirectory));
        Assert.Empty(Directory.EnumerateFileSystemEntries(outsideScope.FullPath));
    }

    private static SkillMaterializedPackageWriteRequest CreateWriteRequest (
        string targetRoot,
        string skillDirectory,
        SkillMaterializedPackage package,
        SkillMaterializedPackageWriteMode writeMode,
        Func<AbsolutePath, CancellationToken, ValueTask<AgentDistributionOperationResult<bool>>>? precondition)
    {
        return new SkillMaterializedPackageWriteRequest(
            AbsolutePath.Parse(targetRoot),
            AbsolutePath.Parse(skillDirectory),
            package,
            writeMode,
            precondition);
    }

    private static SkillMaterializedPackage CreatePackage ()
    {
        return CreatePackage("sample-skill");
    }

    private static SkillMaterializedPackage CreatePackage (string skillName)
    {
        return new SkillMaterializedPackage(
            new SkillName(skillName),
            HostKind.Codex,
            [new PackageTextFile(PackageRelativePath.Parse("SKILL.md"), "# New\n")]);
    }

    private static void AssertSharedLockRootHasNoWorkspaces (string targetRoot)
    {
        var transactionLockRoot = GetTransactionLockRoot(targetRoot);

        Assert.True(Directory.Exists(transactionLockRoot));
        Assert.Empty(Directory.EnumerateDirectories(transactionLockRoot));
    }

    private static string GetTransactionLockRoot (string targetRoot)
    {
        return Path.Combine(targetRoot, ".agent-distribution-skill-transactions");
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

    private sealed class CommitAndRestoreMoveFailingDirectoryOperations : ISkillPackageDirectoryOperations
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
            if (moveCount >= 2)
            {
                throw new IOException("Injected commit or restore move failure.");
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
