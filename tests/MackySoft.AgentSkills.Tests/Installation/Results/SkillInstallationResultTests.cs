using System.Reflection;
using MackySoft.AgentSkills.Hosts.Contracts;
using MackySoft.AgentSkills.Installation.Results;
using MackySoft.AgentSkills.Installation.Targeting;
using MackySoft.AgentSkills.Names;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Tests.Installation.Results;

public sealed class SkillInstallationResultTests
{
    public static TheoryData<Type> ResultDtoTypes => new()
    {
        typeof(SkillActionDiff),
        typeof(SkillActionFileChanges),
        typeof(SkillActionTargetFileSet),
        typeof(SkillActionTargetState),
        typeof(SkillFileDiff),
        typeof(SkillInstallAction),
        typeof(SkillUpdateAction),
        typeof(SkillUninstallAction),
        typeof(SkillPruneAction),
        typeof(SkillInstallResult),
        typeof(SkillUpdateResult),
        typeof(SkillUninstallResult),
        typeof(SkillPruneResult),
    };

    [Theory]
    [Trait("Size", "Small")]
    [MemberData(nameof(ResultDtoTypes))]
    public void ResultDto_ExposesReadOnlyOutputWithoutPublicConstruction (Type type)
    {
        Assert.Empty(type.GetConstructors(BindingFlags.Instance | BindingFlags.Public));
        Assert.All(
            type.GetProperties(BindingFlags.Instance | BindingFlags.Public),
            static property => Assert.Null(property.SetMethod));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void FileChanges_CapturesSortedDisjointPathSnapshots ()
    {
        var replacedFiles = new List<string> { "z.md", "a.md" };
        var removedFiles = new List<string> { "local.md" };
        var changes = new SkillActionFileChanges(replacedFiles, removedFiles);

        replacedFiles.Clear();
        removedFiles.Clear();

        Assert.Equal(["a.md", "z.md"], changes.ReplacedFiles);
        Assert.Equal(["local.md"], changes.RemovedFiles);
        Assert.Throws<ArgumentException>(() => new SkillActionFileChanges(["same.md"], ["same.md"]));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void FileDiff_RequiresContentsThatMatchChangeKind ()
    {
        Assert.Throws<ArgumentException>(() => new SkillFileDiff("SKILL.md", SkillDiffChangeKind.Added, "old", "new"));
        Assert.Throws<ArgumentException>(() => new SkillFileDiff("SKILL.md", SkillDiffChangeKind.Modified, null, "new"));
        Assert.Throws<ArgumentException>(() => new SkillFileDiff("SKILL.md", SkillDiffChangeKind.Deleted, "old", "new"));
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
            fileSet: new SkillActionTargetFileSet(["missing.md"], [], []),
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
        var result = new SkillInstallResult(targetRoot, actions, dryRun: false, force: false, printDiff: false);

        actions.Clear();

        Assert.Single(result.Actions);
        Assert.Throws<ArgumentException>(() => new SkillInstallResult(
            Path.GetFullPath("other-target"),
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
            SkillHostKind.OpenAi,
            SkillScopeKind.Project,
            targetRoot ?? Path.GetFullPath("target"),
            new SkillName("skill-a"));
    }
}
