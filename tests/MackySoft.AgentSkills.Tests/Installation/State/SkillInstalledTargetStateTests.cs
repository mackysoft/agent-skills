using MackySoft.AgentSkills.Installation.State;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Tests.Installation.State;

public sealed class SkillInstalledTargetStateTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void FileSet_CapturesSortedPathSnapshots ()
    {
        var missingFiles = new List<string> { "z.md", "a.md" };
        var fileSet = new SkillInstalledTargetFileSet(missingFiles, [], []);

        missingFiles.Clear();

        Assert.Equal(["a.md", "z.md"], fileSet.MissingFiles);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Blocking_RejectsFailureThatDoesNotMatchKind ()
    {
        var failure = SkillFailure.Create(SkillFailureCodes.InstallTargetHostConflict, "Host conflict.");

        Assert.Throws<ArgumentException>(() => SkillInstalledTargetState.Blocking(
            SkillTargetStateKind.Unmanaged,
            failure));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Drift_RejectsFileSetDriftKind ()
    {
        var failure = SkillFailure.Create(SkillFailureCodes.InstallTargetFileSetMismatch, "File-set drift.");

        Assert.Throws<ArgumentOutOfRangeException>(() => SkillInstalledTargetState.Drift(
            SkillTargetStateKind.FileSetDrift,
            failure,
            bundledSkillBundleVersion: 1));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void FileSetDrift_RejectsFileSetWithoutChangedPaths ()
    {
        var failure = SkillFailure.Create(SkillFailureCodes.InstallTargetFileSetMismatch, "File-set drift.");
        var emptyFileSet = new SkillInstalledTargetFileSet([], [], []);

        Assert.Throws<ArgumentException>(() => SkillInstalledTargetState.FileSetDrift(
            failure,
            emptyFileSet,
            bundledSkillBundleVersion: 1));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void VersionFactories_RejectInvalidVersionRelation ()
    {
        var failure = SkillFailure.Create(SkillFailureCodes.InstallTargetVersionAhead, "Version ahead.");

        Assert.Throws<ArgumentException>(() => SkillInstalledTargetState.VersionAhead(
            failure,
            installedSkillBundleVersion: 1,
            bundledSkillBundleVersion: 1));
        Assert.Throws<ArgumentException>(() => SkillInstalledTargetState.Current(
            installedSkillBundleVersion: 1,
            bundledSkillBundleVersion: 2));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Blocking_RejectsStateNotProducedByInstalledTargetAnalyzer ()
    {
        var failure = SkillFailure.Create(SkillFailureCodes.InstallTargetRemovedFromCatalog, "Removed from catalog.");

        Assert.Throws<ArgumentOutOfRangeException>(() => SkillInstalledTargetState.Blocking(
            SkillTargetStateKind.RemovedFromCatalog,
            failure));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Factories_CreateOnlyTheirCompleteStateShape ()
    {
        var driftFailure = SkillFailure.Create(SkillFailureCodes.InstallTargetContentDigestMismatch, "Content drift.");
        var blockingFailure = SkillFailure.Create(SkillFailureCodes.InstallTargetUnmanaged, "Unmanaged target.");

        var drift = SkillInstalledTargetState.Drift(
            SkillTargetStateKind.CommonContentDrift,
            driftFailure,
            bundledSkillBundleVersion: 1);
        var blocking = SkillInstalledTargetState.Blocking(
            SkillTargetStateKind.Unmanaged,
            blockingFailure);

        Assert.Null(drift.InstalledSkillBundleVersion);
        Assert.Equal(1, drift.BundledSkillBundleVersion);
        Assert.Null(blocking.InstalledSkillBundleVersion);
        Assert.Null(blocking.BundledSkillBundleVersion);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CleanOutdated_AllowsSameVersionWithChangedCanonicalContent ()
    {
        var failure = SkillFailure.Create(SkillFailureCodes.InstallTargetOutdated, "Canonical content changed.");

        var state = SkillInstalledTargetState.CleanOutdated(
            failure,
            installedSkillBundleVersion: 1,
            bundledSkillBundleVersion: 1);

        Assert.Equal(SkillTargetStateKind.CleanOutdated, state.Kind);
    }
}
