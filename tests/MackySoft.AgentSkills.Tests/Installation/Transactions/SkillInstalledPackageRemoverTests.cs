using MackySoft.AgentSkills.Installation.Contracts;
using MackySoft.AgentSkills.Installation.Transactions;
using MackySoft.AgentSkills.Shared;
using MackySoft.Tests;

namespace MackySoft.AgentSkills.Tests.Installation.Transactions;

public sealed class SkillInstalledPackageRemoverTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task DeleteAsync_WhenMovedDirectoryCleanupFails_CommitsDeletionWithoutRestoringPartialTree ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "remover-delete-failure-restores");
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
        var transactionRoot = Path.Combine(targetRoot, ".agent-skills-skill-transactions");
        Assert.True(Directory.Exists(transactionRoot));
        Assert.Contains(Directory.EnumerateDirectories(transactionRoot), static path => path.Contains(".delete.", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task DeleteAsync_WhenMovedTargetPreconditionFails_RestoresTargetAndCleansTransactionDirectory ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "remover-moved-precondition-failure");
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
                    ? SkillOperationResult<bool>.Success(true)
                    : SkillOperationResult<bool>.FailureResult(SkillFailureCodes.InstallTargetDigestMismatch, "Synthetic moved target failure.")),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetDigestMismatch, result.Failure!.Code);
        Assert.True(Directory.Exists(skillDirectory));
        Assert.Equal("# Existing\n", File.ReadAllText(skillPath));
        Assert.False(Directory.Exists(Path.Combine(targetRoot, ".agent-skills-skill-transactions")));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task DeleteAsync_WithPreconditionWhenTargetIsMissing_ReturnsChangedTargetFailure ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "remover-missing-precondition");
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
                return ValueTask.FromResult(SkillOperationResult<bool>.Success(true));
            },
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetDigestMismatch, result.Failure!.Code);
        Assert.Equal(1, preconditionCallCount);
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
            if (path.Value.Contains(".delete.", StringComparison.Ordinal))
            {
                File.Delete(Path.Combine(path.Value, "SKILL.md"));
                throw new IOException("Injected moved directory delete failure.");
            }

            if (path.Value.Contains(".agent-skills-skill-transactions", StringComparison.Ordinal))
            {
                throw new IOException("Injected transaction cleanup failure.");
            }

            Directory.Delete(path.Value, recursive);
        }
    }
}
