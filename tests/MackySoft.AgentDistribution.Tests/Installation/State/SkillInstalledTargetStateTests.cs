using MackySoft.AgentDistribution.Bundles;
using MackySoft.AgentDistribution.Installation.State;
using MackySoft.AgentDistribution.Shared;

namespace MackySoft.AgentDistribution.Tests.Installation.State;

public sealed class SkillInstalledTargetStateTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void FileSet_CapturesSortedPathSnapshots ()
    {
        var missingFiles = new List<PackageRelativePath>
        {
            PackageRelativePath.Parse("z.md"),
            PackageRelativePath.Parse("a.md"),
        };
        var fileSet = new SkillInstalledTargetFileSet(missingFiles, [], []);

        missingFiles.Clear();

        Assert.Equal(
            [PackageRelativePath.Parse("a.md"), PackageRelativePath.Parse("z.md")],
            fileSet.MissingFiles);
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
            bundledSkillBundleVersion: Version(1)));
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
            bundledSkillBundleVersion: Version(1)));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void VersionFactories_RejectInvalidVersionRelation ()
    {
        var failure = SkillFailure.Create(SkillFailureCodes.InstallTargetVersionAhead, "Version ahead.");

        Assert.Throws<ArgumentException>(() => SkillInstalledTargetState.VersionAhead(
            failure,
            installedSkillBundleVersion: Version(1),
            bundledSkillBundleVersion: Version(1)));
        Assert.Throws<ArgumentException>(() => SkillInstalledTargetState.Current(
            installedSkillBundleVersion: Version(1),
            bundledSkillBundleVersion: Version(2)));
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
            bundledSkillBundleVersion: Version(1));
        var blocking = SkillInstalledTargetState.Blocking(
            SkillTargetStateKind.Unmanaged,
            blockingFailure);

        Assert.Null(drift.InstalledSkillBundleVersion);
        Assert.Equal(1, drift.BundledSkillBundleVersion!.Value);
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
            installedSkillBundleVersion: Version(1),
            bundledSkillBundleVersion: Version(1));

        Assert.Equal(SkillTargetStateKind.CleanOutdated, state.Kind);
    }

    private static SkillBundleVersion Version (int value)
    {
        return new SkillBundleVersion(value);
    }
}
