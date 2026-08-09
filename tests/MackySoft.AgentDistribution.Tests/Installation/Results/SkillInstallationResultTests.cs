using MackySoft.AgentDistribution.Installation.Results;
using MackySoft.AgentDistribution.Installation.Targeting;
using MackySoft.AgentDistribution.Shared;

namespace MackySoft.AgentDistribution.Tests.Installation.Results;

public sealed class SkillInstallationResultTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void FileChanges_CapturesSortedDisjointPathSnapshots ()
    {
        var replacedFiles = new List<PackageRelativePath>
        {
            PackageRelativePath.Parse("z.md"),
            PackageRelativePath.Parse("a.md"),
        };
        var removedFiles = new List<PackageRelativePath> { PackageRelativePath.Parse("local.md") };
        var changes = new SkillActionFileChanges(replacedFiles, removedFiles);

        replacedFiles.Clear();
        removedFiles.Clear();

        Assert.Equal([PackageRelativePath.Parse("a.md"), PackageRelativePath.Parse("z.md")], changes.ReplacedFiles);
        Assert.Equal([PackageRelativePath.Parse("local.md")], changes.RemovedFiles);
        var duplicatePath = PackageRelativePath.Parse("same.md");
        Assert.Throws<ArgumentException>(() => new SkillActionFileChanges([duplicatePath], [duplicatePath]));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void FileDiff_RequiresContentsThatMatchChangeKind ()
    {
        var path = PackageRelativePath.Parse("SKILL.md");
        Assert.Throws<ArgumentException>(() => new SkillFileDiff(path, SkillDiffChangeKind.Added, "old", "new"));
        Assert.Throws<ArgumentException>(() => new SkillFileDiff(path, SkillDiffChangeKind.Modified, null, "new"));
        Assert.Throws<ArgumentException>(() => new SkillFileDiff(path, SkillDiffChangeKind.Deleted, "old", "new"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TargetState_RejectsContradictoryFailureFileSetAndVersionStates ()
    {
        Assert.Throws<ArgumentException>(() => new SkillActionTargetState(
            SkillTargetStateKind.Current,
            SkillFailureCodes.InstallTargetContentDigestMismatch,
            "Drift.",
            fileSet: null,
            installedSkillBundleVersion: 1,
            bundledSkillBundleVersion: 1));
        Assert.Throws<ArgumentException>(() => new SkillActionTargetState(
            SkillTargetStateKind.CommonContentDrift,
            SkillFailureCodes.InstallTargetContentDigestMismatch,
            "Content drift.",
            fileSet: new SkillActionTargetFileSet([PackageRelativePath.Parse("missing.md")], [], []),
            installedSkillBundleVersion: null,
            bundledSkillBundleVersion: 1));
        Assert.Throws<ArgumentException>(() => new SkillActionTargetState(
            SkillTargetStateKind.VersionAhead,
            SkillFailureCodes.InstallTargetVersionAhead,
            "Version ahead.",
            fileSet: null,
            installedSkillBundleVersion: 1,
            bundledSkillBundleVersion: 1));
        Assert.Throws<ArgumentException>(() => new SkillActionTargetState(
            SkillTargetStateKind.FileSetDrift,
            SkillFailureCodes.InstallTargetFileSetMismatch,
            "File-set drift.",
            fileSet: new SkillActionTargetFileSet([], [], []),
            installedSkillBundleVersion: null,
            bundledSkillBundleVersion: 1));
        Assert.Throws<ArgumentException>(() => new SkillActionTargetState(
            SkillTargetStateKind.CommonContentDrift,
            SkillFailureCodes.InstallTargetContentDigestMismatch,
            "Content drift.",
            fileSet: null,
            installedSkillBundleVersion: null,
            bundledSkillBundleVersion: null));
        Assert.Throws<ArgumentException>(() => new SkillActionTargetState(
            SkillTargetStateKind.CommonContentDrift,
            SkillFailureCodes.InstallTargetContentDigestMismatch,
            "Content drift.",
            fileSet: null,
            installedSkillBundleVersion: 1,
            bundledSkillBundleVersion: 2));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ActionConstructors_RejectActionKindStateContradictions ()
    {
        var identity = CreateIdentity();
        var emptyChanges = new SkillActionFileChanges([], []);

        Assert.Throws<ArgumentException>(() => new SkillInstallAction(
            identity,
            SkillInstallActionKind.Created,
            CreateCurrentState(),
            blockedReason: null,
            diffs: [],
            emptyChanges));
        Assert.Throws<ArgumentException>(() => new SkillUpdateAction(
            identity,
            SkillUpdateActionKind.BlockedUnmanaged,
            CreateUnmanagedState(),
            SkillBlockedReason.LocalModificationRequiresForce,
            diffs: [],
            fileChanges: null));
        Assert.Throws<ArgumentNullException>(() => new SkillUninstallAction(
            identity,
            SkillUninstallActionKind.Deleted,
            CreateCurrentState(),
            blockedReason: null,
            fileChanges: null));
        Assert.Throws<ArgumentException>(() => new SkillPruneAction(
            identity,
            SkillPruneActionKind.SkippedCurrent,
            CreateCurrentState(),
            blockedReason: null,
            fileChanges: null));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Result_CapturesActionsAndRequiresMatchingTargetRoot ()
    {
        var targetRoot = Path.GetFullPath("target");
        var actions = new List<SkillInstallAction>
        {
            new(CreateIdentity(targetRoot), SkillInstallActionKind.NoOp, CreateCurrentState(), null, null, null),
        };
        var result = new SkillInstallResult(AbsolutePath.Parse(targetRoot), actions, dryRun: false, force: false, printDiff: false);

        actions.Clear();

        Assert.Single(result.Actions);
        Assert.Throws<ArgumentException>(() => new SkillInstallResult(
            AbsolutePath.Parse(Path.GetFullPath("other-target")),
            result.Actions,
            dryRun: false,
            force: false,
            printDiff: false));
    }

    private static SkillActionTargetState CreateCurrentState ()
    {
        return new SkillActionTargetState(
            SkillTargetStateKind.Current,
            code: null,
            message: null,
            fileSet: null,
            installedSkillBundleVersion: 1,
            bundledSkillBundleVersion: 1);
    }

    private static SkillActionTargetState CreateUnmanagedState ()
    {
        return new SkillActionTargetState(
            SkillTargetStateKind.Unmanaged,
            SkillFailureCodes.InstallTargetUnmanaged,
            "Unmanaged.",
            fileSet: null,
            installedSkillBundleVersion: null,
            bundledSkillBundleVersion: null);
    }

    private static SkillInstallIdentity CreateIdentity (string? targetRoot = null)
    {
        return new SkillInstallIdentity(
            HostKind.Codex,
            SkillScopeKind.Project,
            AbsolutePath.Parse(targetRoot ?? Path.GetFullPath("target")),
            new SkillName("skill-a"));
    }
}
