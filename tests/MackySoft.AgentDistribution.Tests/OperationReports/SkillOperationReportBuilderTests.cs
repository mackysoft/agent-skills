using MackySoft.AgentDistribution.Digests;
using MackySoft.AgentDistribution.Distribution;
using MackySoft.AgentDistribution.Doctor;
using MackySoft.AgentDistribution.Installation.Results;
using MackySoft.AgentDistribution.Installation.Targeting;
using MackySoft.AgentDistribution.OperationReports.Contracts;
using MackySoft.AgentDistribution.OperationReports.Literals;
using MackySoft.AgentDistribution.OperationReports.Projection;
using MackySoft.AgentDistribution.Shared;
using MackySoft.AgentDistribution.Skills.Manifests;

namespace MackySoft.AgentDistribution.Tests.OperationReports;

public sealed class SkillOperationReportBuilderTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void CreateInstallReport_ProjectsActionsCountsAndFileDetails ()
    {
        var targetRoot = Path.GetFullPath("install-report-target");
        var context = CreateContext(
            [new SkillCategory("basic"), new SkillCategory("advanced")],
            [new SkillName("skill-a"), new SkillName("skill-c")]);
        var result = new SkillInstallResult(
            AbsolutePath.Parse(targetRoot),
            [
                new SkillInstallAction(
                    CreateIdentity(targetRoot, "skill-b"),
                    SkillInstallActionKind.NoOp,
                    CreateCurrentTargetState(),
                    blockedReason: null,
                    diffs: null,
                    fileChanges: null),
                new SkillInstallAction(
                    CreateIdentity(targetRoot, "skill-c"),
                    SkillInstallActionKind.BlockedLocalModification,
                    new SkillActionTargetState(
                        SkillTargetStateKind.FileSetDrift,
                        AgentDistributionFailureCodes.InstallTargetFileSetMismatch,
                        "File set drift.",
                        new SkillActionTargetFileSet(
                            [PackageRelativePath.Parse("missing.md")],
                            [PackageRelativePath.Parse("extra.md")],
                            [PackageRelativePath.Parse("extra-dir")]),
                        installedSkillBundleVersion: null,
                        bundledSkillBundleVersion: 1),
                    SkillBlockedReason.LocalModificationRequiresForce,
                    diffs: [],
                    fileChanges: null),
                new SkillInstallAction(
                    CreateIdentity(targetRoot, "skill-a"),
                    SkillInstallActionKind.Updated,
                    new SkillActionTargetState(
                        SkillTargetStateKind.CommonContentDrift,
                        AgentDistributionFailureCodes.InstallTargetContentDigestMismatch,
                        "Content drift.",
                        fileSet: null,
                        installedSkillBundleVersion: null,
                        bundledSkillBundleVersion: 2),
                    blockedReason: null,
                    diffs:
                    [
                        new SkillActionDiff(
                        [
                            new SkillFileDiff(PackageRelativePath.Parse("z.txt"), SkillDiffChangeKind.Deleted, "old", null),
                            new SkillFileDiff(PackageRelativePath.Parse("a.txt"), SkillDiffChangeKind.Added, null, "new"),
                        ]),
                    ],
                    fileChanges: new SkillActionFileChanges(
                        [PackageRelativePath.Parse("z.txt"), PackageRelativePath.Parse("a.txt")],
                        [PackageRelativePath.Parse("local.md")])),
            ],
            dryRun: true,
            force: true,
            printDiff: true);

        var report = SkillOperationReportBuilder.CreateInstallReport(result, context);

        Assert.Equal(HostKind.Codex, report.Host);
        Assert.Equal(["basic", "advanced"], report.Categories);
        Assert.Equal(["skill-a", "skill-c"], report.SkillNames);
        Assert.Equal(OperationScopeKind.Project, report.Scope);
        Assert.Equal(targetRoot, report.TargetRoot);
        Assert.True(report.DryRun);
        Assert.True(report.Force);
        Assert.Equal(CodexHost.ReloadGuidance, report.ReloadGuidance);
        Assert.Equal(["skill-a", "skill-b", "skill-c"], report.Actions.Select(static action => action.SkillName).ToArray());

        var updated = report.Actions[0];
        Assert.Equal("updated", updated.Action);
        Assert.Equal(OperationActionStatus.Changed, updated.Status);
        Assert.Null(updated.BlockedReason);
        Assert.Equal(SkillTargetStateKind.CommonContentDrift, updated.TargetState!.Kind);
        Assert.Equal(AgentDistributionFailureCodes.InstallTargetContentDigestMismatch.Value, updated.TargetState.Code);
        Assert.Null(updated.TargetState.InstalledSkillBundleVersion);
        Assert.Equal(2, updated.TargetState.BundledSkillBundleVersion);
        Assert.Equal(["a.txt", "z.txt"], updated.FileChanges!.ReplacedFiles);
        Assert.Equal(["local.md"], updated.FileChanges.RemovedFiles);
        Assert.Equal(["a.txt", "z.txt"], updated.FileDiffs.Select(static diff => diff.RelativePath).ToArray());
        Assert.Equal(SkillDiffChangeKind.Added, updated.FileDiffs[0].ChangeKind);
        Assert.Equal(SkillDiffChangeKind.Deleted, updated.FileDiffs[1].ChangeKind);

        var blocked = report.Actions[2];
        Assert.Equal("blockedLocalModification", blocked.Action);
        Assert.Equal(OperationActionStatus.Blocked, blocked.Status);
        Assert.Equal(SkillBlockedReason.LocalModificationRequiresForce, blocked.BlockedReason);
        Assert.Equal(SkillTargetStateKind.FileSetDrift, blocked.TargetState!.Kind);
        Assert.Equal(["missing.md"], blocked.TargetState.FileSet!.MissingFiles);

        Assert.Equal(
            ["created", "updated", "noOp", "blockedManagedOverwrite", "blockedLocalModification", "blockedUnmanaged"],
            report.ActionCounts.Select(static count => count.Literal).ToArray());
        Assert.Equal(
            ["changed", "noOp", "skipped", "blocked"],
            report.StatusCounts.Select(static count => count.Literal).ToArray());
        AssertCount(report.ActionCounts, "created", 0);
        AssertCount(report.ActionCounts, "updated", 1);
        AssertCount(report.ActionCounts, "noOp", 1);
        AssertCount(report.ActionCounts, "blockedLocalModification", 1);
        AssertCount(report.StatusCounts, "changed", 1);
        AssertCount(report.StatusCounts, "noOp", 1);
        AssertCount(report.StatusCounts, "skipped", 0);
        AssertCount(report.StatusCounts, "blocked", 1);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CreateUpdateReport_ProjectsBlockedUnmanagedCounts ()
    {
        var targetRoot = Path.GetFullPath("update-report-target");
        var context = CreateContext(SkillScopeKind.User);
        var result = new SkillUpdateResult(
            AbsolutePath.Parse(targetRoot),
            [
                new SkillUpdateAction(
                    CreateIdentity(targetRoot, "skill-b", SkillScopeKind.User),
                    SkillUpdateActionKind.BlockedUnmanaged,
                    CreateUnmanagedTargetState(),
                    SkillBlockedReason.UnmanagedTarget,
                    diffs: [],
                    fileChanges: null),
                new SkillUpdateAction(
                    CreateIdentity(targetRoot, "skill-a", SkillScopeKind.User),
                    SkillUpdateActionKind.Created,
                    CreateMissingTargetState(),
                    blockedReason: null,
                    diffs:
                    [
                        new SkillActionDiff(
                        [
                            new SkillFileDiff(PackageRelativePath.Parse("SKILL.md"), SkillDiffChangeKind.Modified, "old", "new"),
                        ]),
                    ],
                    fileChanges: new SkillActionFileChanges([], [])),
            ],
            dryRun: true,
            force: false,
            printDiff: false);

        var report = SkillOperationReportBuilder.CreateUpdateReport(result, context);

        Assert.Equal(OperationScopeKind.User, report.Scope);
        Assert.Equal(["skill-a", "skill-b"], report.Actions.Select(static action => action.SkillName).ToArray());
        Assert.Equal("created", report.Actions[0].Action);
        Assert.Equal(OperationActionStatus.Changed, report.Actions[0].Status);
        Assert.Empty(report.Actions[0].FileDiffs);
        Assert.Equal("blockedUnmanaged", report.Actions[1].Action);
        Assert.Equal(OperationActionStatus.Blocked, report.Actions[1].Status);
        Assert.Equal(SkillBlockedReason.UnmanagedTarget, report.Actions[1].BlockedReason);
        Assert.Equal(
            ["created", "updated", "noOp", "blockedLocalModification", "blockedUnmanaged", "blockedVersionAhead"],
            report.ActionCounts.Select(static count => count.Literal).ToArray());
        Assert.Equal(
            ["changed", "noOp", "skipped", "blocked"],
            report.StatusCounts.Select(static count => count.Literal).ToArray());
        AssertCount(report.ActionCounts, "created", 1);
        AssertCount(report.ActionCounts, "blockedUnmanaged", 1);
        AssertCount(report.StatusCounts, "changed", 1);
        AssertCount(report.StatusCounts, "blocked", 1);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CreateUninstallReport_ProjectsSkippedAndDeletedActions ()
    {
        var targetRoot = Path.GetFullPath("uninstall-report-target");
        var context = CreateContext();
        var result = new SkillUninstallResult(
            AbsolutePath.Parse(targetRoot),
            [
                new SkillUninstallAction(
                    CreateIdentity(targetRoot, "skill-b"),
                    SkillUninstallActionKind.SkippedUnmanaged,
                    CreateUnmanagedTargetState(),
                    blockedReason: null,
                    fileChanges: null),
                new SkillUninstallAction(
                    CreateIdentity(targetRoot, "skill-a"),
                    SkillUninstallActionKind.Deleted,
                    CreateCurrentTargetState(),
                    blockedReason: null,
                    fileChanges: new SkillActionFileChanges(
                        [],
                        [PackageRelativePath.Parse("SKILL.md"), PackageRelativePath.Parse("agent-skill.json")])),
            ],
            dryRun: false,
            force: true);

        var report = SkillOperationReportBuilder.CreateUninstallReport(result, context);

        Assert.Equal("deleted", report.Actions[0].Action);
        Assert.Equal(OperationActionStatus.Changed, report.Actions[0].Status);
        Assert.Equal(["SKILL.md", "agent-skill.json"], report.Actions[0].FileChanges!.RemovedFiles);
        Assert.Equal("skippedUnmanaged", report.Actions[1].Action);
        Assert.Equal(OperationActionStatus.Skipped, report.Actions[1].Status);
        Assert.Equal(
            ["deleted", "noOp", "skippedUnmanaged", "blockedLocalModification"],
            report.ActionCounts.Select(static count => count.Literal).ToArray());
        Assert.Equal(
            ["changed", "noOp", "skipped", "blocked"],
            report.StatusCounts.Select(static count => count.Literal).ToArray());
        AssertCount(report.ActionCounts, "deleted", 1);
        AssertCount(report.ActionCounts, "skippedUnmanaged", 1);
        AssertCount(report.StatusCounts, "changed", 1);
        AssertCount(report.StatusCounts, "skipped", 1);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CreatePruneReport_ProjectsActionsCountsAndFileDetails ()
    {
        var targetRoot = Path.GetFullPath("prune-report-target");
        var context = CreateContext();
        var result = new SkillPruneResult(
            AbsolutePath.Parse(targetRoot),
            [
                new SkillPruneAction(
                    CreateIdentity(targetRoot, "skill-a"),
                    SkillPruneActionKind.Deleted,
                    new SkillActionTargetState(
                        SkillTargetStateKind.RemovedFromCatalog,
                        AgentDistributionFailureCodes.InstallTargetRemovedFromCatalog,
                        "Removed from catalog.",
                        fileSet: null,
                        installedSkillBundleVersion: 1,
                        bundledSkillBundleVersion: null),
                    blockedReason: null,
                    fileChanges: new SkillActionFileChanges(
                        [],
                        [PackageRelativePath.Parse("SKILL.md"), PackageRelativePath.Parse("agent-skill.json")])),
                new SkillPruneAction(CreateIdentity(targetRoot, "skill-b"), SkillPruneActionKind.SkippedCurrent, null, null, null),
                new SkillPruneAction(CreateIdentity(targetRoot, "skill-c"), SkillPruneActionKind.SkippedForeignCatalog, null, null, null),
                new SkillPruneAction(
                    CreateIdentity(targetRoot, "skill-d"),
                    SkillPruneActionKind.SkippedUnmanaged,
                    new SkillActionTargetState(
                        SkillTargetStateKind.Unmanaged,
                        AgentDistributionFailureCodes.InstallTargetUnmanaged,
                        "Unmanaged.",
                        fileSet: null,
                        installedSkillBundleVersion: null,
                        bundledSkillBundleVersion: null),
                    blockedReason: null,
                    fileChanges: null),
                new SkillPruneAction(
                    CreateIdentity(targetRoot, "skill-e"),
                    SkillPruneActionKind.BlockedLocalModification,
                    new SkillActionTargetState(
                        SkillTargetStateKind.CommonContentDrift,
                        AgentDistributionFailureCodes.InstallTargetContentDigestMismatch,
                        "Content drift.",
                        fileSet: null,
                        installedSkillBundleVersion: 1,
                        bundledSkillBundleVersion: null),
                    SkillBlockedReason.LocalModificationRequiresForce,
                    fileChanges: null),
                new SkillPruneAction(
                    CreateIdentity(targetRoot, "skill-f"),
                    SkillPruneActionKind.BlockedManifestInvalid,
                    new SkillActionTargetState(
                        SkillTargetStateKind.ManifestDrift,
                        AgentDistributionFailureCodes.ManifestInvalid,
                        "Invalid manifest.",
                        fileSet: null,
                        installedSkillBundleVersion: null,
                        bundledSkillBundleVersion: null),
                    blockedReason: null,
                    fileChanges: null),
                new SkillPruneAction(
                    CreateIdentity(targetRoot, "skill-g"),
                    SkillPruneActionKind.BlockedNameCollision,
                    new SkillActionTargetState(
                        SkillTargetStateKind.NameCollision,
                        AgentDistributionFailureCodes.InstallTargetNameCollision,
                        "Name collision.",
                        fileSet: null,
                        installedSkillBundleVersion: null,
                        bundledSkillBundleVersion: null),
                    blockedReason: null,
                    fileChanges: null),
                new SkillPruneAction(
                    CreateIdentity(targetRoot, "skill-h"),
                    SkillPruneActionKind.BlockedHostConflict,
                    new SkillActionTargetState(
                        SkillTargetStateKind.HostConflict,
                        AgentDistributionFailureCodes.InstallTargetHostConflict,
                        "Host conflict.",
                        fileSet: null,
                        installedSkillBundleVersion: null,
                        bundledSkillBundleVersion: null),
                    blockedReason: null,
                    fileChanges: null),
            ],
            dryRun: true,
            force: false);

        var report = SkillOperationReportBuilder.CreatePruneReport(result, context);

        Assert.True(report.DryRun);
        Assert.False(report.Force);
        Assert.Equal(
            ["skill-a", "skill-b", "skill-c", "skill-d", "skill-e", "skill-f", "skill-g", "skill-h"],
            report.Actions.Select(static action => action.SkillName).ToArray());
        Assert.Equal(
            ["deleted", "skippedCurrent", "skippedForeignCatalog", "skippedUnmanaged", "blockedLocalModification", "blockedManifestInvalid", "blockedNameCollision", "blockedHostConflict"],
            report.Actions.Select(static action => action.Action).ToArray());
        Assert.Equal(
            [
                OperationActionStatus.Changed,
                OperationActionStatus.NoOp,
                OperationActionStatus.Skipped,
                OperationActionStatus.Skipped,
                OperationActionStatus.Blocked,
                OperationActionStatus.Blocked,
                OperationActionStatus.Blocked,
                OperationActionStatus.Blocked,
            ],
            report.Actions.Select(static action => action.Status).ToArray());
        var deletedTargetState = report.Actions[0].TargetState!;
        Assert.Equal(SkillTargetStateKind.RemovedFromCatalog, deletedTargetState.Kind);
        Assert.Equal(AgentDistributionFailureCodes.InstallTargetRemovedFromCatalog.Value, deletedTargetState.Code);
        Assert.Equal(["SKILL.md", "agent-skill.json"], report.Actions[0].FileChanges!.RemovedFiles);
        Assert.Equal(SkillBlockedReason.LocalModificationRequiresForce, report.Actions[4].BlockedReason);
        Assert.Equal(
            [
                "deleted",
                "skippedCurrent",
                "skippedForeignCatalog",
                "skippedUnmanaged",
                "blockedLocalModification",
                "blockedManifestInvalid",
                "blockedNameCollision",
                "blockedHostConflict",
            ],
            report.ActionCounts.Select(static count => count.Literal).ToArray());
        AssertCount(report.ActionCounts, "deleted", 1);
        AssertCount(report.ActionCounts, "skippedCurrent", 1);
        AssertCount(report.ActionCounts, "skippedForeignCatalog", 1);
        AssertCount(report.ActionCounts, "skippedUnmanaged", 1);
        AssertCount(report.ActionCounts, "blockedLocalModification", 1);
        AssertCount(report.ActionCounts, "blockedManifestInvalid", 1);
        AssertCount(report.ActionCounts, "blockedNameCollision", 1);
        AssertCount(report.ActionCounts, "blockedHostConflict", 1);
        AssertCount(report.StatusCounts, "changed", 1);
        AssertCount(report.StatusCounts, "noOp", 1);
        AssertCount(report.StatusCounts, "skipped", 2);
        AssertCount(report.StatusCounts, "blocked", 4);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task CreateListReport_UsesCanonicalSkillAndHostDescriptorData ()
    {
        var bundle = await SkillTestData.GenerateFixtureBundleAsync();
        var packages = bundle.Packages.Reverse().ToArray();
        var catalog = new SkillPackageCatalog(
            bundle.Descriptor,
            [new SkillCategory("core")],
            [packages[0].Manifest.SkillName],
            [new SkillCategoryPackageCount(new SkillCategory("core"), packages.Length)],
            packages);

        var report = SkillOperationReportBuilder.CreateListReport(catalog, SupportedHosts);

        Assert.Equal(["core"], report.Categories);
        Assert.Equal([packages[0].Manifest.SkillName.Value], report.SkillNames);
        Assert.Equal(["core"], report.AvailableCategories.Select(static category => category.Category).ToArray());
        Assert.Equal([packages.Length], report.AvailableCategories.Select(static category => category.SkillCount).ToArray());
        Assert.Equal(SkillTestData.ExpectedSkillNames, report.Skills.Select(static skill => skill.SkillName).ToArray());
        Assert.All(report.Skills, static skill => Assert.Empty(skill.Dependencies));
        Assert.All(report.Skills, static skill => Assert.Equal("core", skill.Category));
        Assert.All(report.Skills, static skill => Assert.Equal(1, skill.SkillBundleVersion));
        Assert.All(report.Skills, static skill => Assert.Equal("com.mackysoft.agent-distribution", skill.CatalogId));
        Assert.Equal([HostKind.Codex, HostKind.ClaudeCode, HostKind.GitHubCopilot], report.SupportedHosts.Select(static host => host.Host).ToArray());
        var codex = report.SupportedHosts.Single(static host => host.Host == HostKind.Codex);
        Assert.Equal("catalog-directory", codex.BundleTargetRootLayout);
        Assert.Equal(["flat"], codex.CompatiblePreviousBundleTargetRootLayouts);
        Assert.Equal("agents/openai.yaml", codex.MetadataArtifactPath);
        Assert.Equal(
            [HostKind.Codex, HostKind.ClaudeCode, HostKind.GitHubCopilot],
            report.Skills[0].HostArtifacts.Select(static artifact => artifact.Host).ToArray());
        var firstPackage = packages.Single(package => string.Equals(package.Manifest.SkillName.Value, report.Skills[0].SkillName, StringComparison.Ordinal));
        Assert.Equal(firstPackage.Manifest.ContentDigest, report.Skills[0].ContentDigest);
        Assert.Equal(firstPackage.Manifest.ManifestDigest, report.Skills[0].ManifestDigest);
        Assert.Equal(
            firstPackage.Manifest.HostArtifacts.Select(static artifact => (artifact.Digest, artifact.MaterializedFrontmatterDigest)),
            report.Skills[0].HostArtifacts.Select(static artifact => (artifact.Digest, artifact.MaterializedFrontmatterDigest)));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task CreateListReport_ProjectsSkillDependencies ()
    {
        var bundle = await SkillTestData.GenerateFixtureBundleAsync();
        var packages = bundle.Packages.ToArray();
        var manifest = SkillTestData.WithComputedManifestDigest(SkillTestData.CopyManifest(
            packages[0].Manifest,
            dependencies: [packages[1].Manifest.SkillName]));
        var serializer = new SkillManifestJsonSerializer();
        var files = packages[0].Files
            .Select(file => string.Equals(file.RelativePath.Value, "agent-skill.json", StringComparison.Ordinal)
                ? new PackageTextFile(file.RelativePath, serializer.Serialize(manifest))
                : file)
            .ToArray();
        packages[0] = SkillTestData.CreateCanonicalPackage(manifest, files);
        var catalog = new SkillPackageCatalog(
            bundle.Descriptor,
            [new SkillCategory("core")],
            [],
            [new SkillCategoryPackageCount(new SkillCategory("core"), packages.Length)],
            packages);

        var report = SkillOperationReportBuilder.CreateListReport(catalog, SupportedHosts);

        var skill = report.Skills.Single(skill => string.Equals(skill.SkillName, packages[0].Manifest.SkillName.Value, StringComparison.Ordinal));
        Assert.Equal([packages[1].Manifest.SkillName.Value], skill.Dependencies);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task CreateExportReport_ProjectsFormatAndSortedSkillNames ()
    {
        var packages = (await SkillTestData.GenerateFixturePackagesAsync()).Reverse().ToArray();
        var outputPath = Path.Combine("tmp", "agent-distribution.zip");

        var report = SkillOperationReportBuilder.CreateExportReport(
            AbsolutePath.Parse(Path.GetFullPath(outputPath)),
            packages,
            CodexHost,
            PackageExportFormat.Zip,
            [new SkillCategory("basic"), new SkillCategory("advanced")],
            [packages[0].Manifest.SkillName]);

        Assert.Equal(HostKind.Codex, report.Host);
        Assert.Equal(["basic", "advanced"], report.Categories);
        Assert.Equal([packages[0].Manifest.SkillName.Value], report.SkillNames);
        Assert.Equal(PackageExportFormat.Zip, report.Format);
        Assert.Equal(Path.GetFullPath(outputPath), report.OutputPath);
        Assert.Equal(SkillTestData.ExpectedSkillNames, report.Skills);
        Assert.Equal(SkillTestData.ExpectedSkillNames.Length, report.SkillCount);
        Assert.Equal(CodexHost.ReloadGuidance, report.ReloadGuidance);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CreateDoctorReport_ProjectsSeverityAndTargetStateFromDiagnostics ()
    {
        var targetRoot = Path.GetFullPath("agent-distribution-doctor");
        var result = new SkillDoctorResult(
            HostKind.Codex,
            AbsolutePath.Parse(targetRoot),
            [
                SkillDoctorDiagnostic.Error(
                    AgentDistributionFailureCodes.InstallTargetHostArtifactDigestMismatch,
                    "Host artifact drift.",
                    "skill-b"),
                SkillDoctorDiagnostic.Error(
                    AgentDistributionFailureCodes.InstallTargetUnmanaged,
                    "Target root is missing."),
                SkillDoctorDiagnostic.Error(
                    AgentDistributionFailureCodes.InstallTargetVersionAhead,
                    "Version ahead.",
                    "skill-c"),
                SkillDoctorDiagnostic.Error(
                    AgentDistributionFailureCodes.InstallTargetOutdated,
                    "Clean outdated.",
                    "skill-d"),
                SkillDoctorDiagnostic.Error(
                    AgentDistributionFailureCodes.InstallTargetRemovedFromCatalog,
                    "Removed from catalog.",
                    "skill-e"),
                SkillDoctorDiagnostic.Error(
                    "SKILL_DOCTOR_SHARED",
                    "Same diagnostic.",
                    "skill-a"),
                SkillDoctorDiagnostic.Info(
                    "SKILL_DOCTOR_SHARED",
                    "Same diagnostic.",
                    "skill-a"),
                SkillDoctorDiagnostic.Info(
                    "SKILL_DOCTOR_OK",
                    "Healthy.",
                    "skill-a"),
            ]);

        var report = SkillOperationReportBuilder.CreateDoctorReport(
            result,
            new SkillOperationReportContext(
                CodexHost,
                SkillScopeKind.Project,
                Path.GetFullPath("."),
                [new SkillCategory("developer")],
                [new SkillName("skill-a")]));

        Assert.False(report.IsHealthy);
        Assert.Equal(["developer"], report.Categories);
        Assert.Equal(["skill-a"], report.SkillNames);
        Assert.Equal(OperationScopeKind.Project, report.Scope);
        Assert.Equal(Path.GetFullPath("."), report.RepositoryRoot);
        Assert.Equal(CodexHost.ReloadGuidance, report.ReloadGuidance);
        Assert.Equal(new string?[] { null, "skill-a", "skill-a", "skill-a", "skill-b", "skill-c", "skill-d", "skill-e" }, report.Diagnostics.Select(static diagnostic => diagnostic.SkillName).ToArray());
        Assert.Equal(SkillDoctorSeverity.Error, report.Diagnostics[0].Severity);
        Assert.Null(report.Diagnostics[0].TargetState);
        Assert.Equal(SkillDoctorSeverity.Info, report.Diagnostics[1].Severity);
        Assert.Null(report.Diagnostics[1].TargetState);
        Assert.Equal(SkillDoctorSeverity.Info, report.Diagnostics[2].Severity);
        Assert.Equal(SkillDoctorSeverity.Error, report.Diagnostics[3].Severity);
        Assert.Equal(SkillTargetStateKind.HostArtifactDrift, report.Diagnostics[4].TargetState);
        Assert.Equal(SkillTargetStateKind.VersionAhead, report.Diagnostics[5].TargetState);
        Assert.Equal(SkillTargetStateKind.CleanOutdated, report.Diagnostics[6].TargetState);
        Assert.Equal(SkillTargetStateKind.RemovedFromCatalog, report.Diagnostics[7].TargetState);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void OperationReportPublicContracts_DoNotExposeProductEnvelopeFields ()
    {
        var forbiddenTerms = new[] { "command", "exitCode", "ucli", "dotmet" };
        var reportTypes = GetPublicReportContractTypes();

        Assert.NotEmpty(reportTypes);
        foreach (var reportType in reportTypes)
        {
            Assert.DoesNotContain(forbiddenTerms, term => reportType.Name.Contains(term, StringComparison.OrdinalIgnoreCase));
            foreach (var property in reportType.GetProperties())
            {
                Assert.DoesNotContain(forbiddenTerms, term => property.Name.Contains(term, StringComparison.OrdinalIgnoreCase));
            }
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void OperationReportPublicContracts_DoNotExposeOperationSourceModels ()
    {
        var reportTypes = GetPublicReportContractTypes();

        Assert.NotEmpty(reportTypes);
        var exposedSourceTypes = reportTypes
            .SelectMany(static reportType => reportType
                .GetProperties()
                .SelectMany(property => GetUnsupportedPropertyTypes(property.PropertyType)
                    .Select(type => $"{reportType.Name}.{property.Name}: {type.FullName}")))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(exposedSourceTypes);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CreateInstallReport_RejectsMismatchedContext ()
    {
        var targetRoot = Path.GetFullPath("install-report-context-mismatch");
        var result = new SkillInstallResult(
            AbsolutePath.Parse(targetRoot),
            [CreateNoOpInstallAction(CreateIdentity(targetRoot, "skill-a"))],
            dryRun: false,
            force: false,
            printDiff: false);

        Assert.Throws<ArgumentException>(() => SkillOperationReportBuilder.CreateInstallReport(
            result,
            new SkillOperationReportContext(
                ResolveHost(HostKind.ClaudeCode),
                SkillScopeKind.Project,
                Path.GetFullPath("."),
                [new SkillCategory("basic")],
                [])));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CreateDoctorReport_RejectsMismatchedContextHost ()
    {
        var result = new SkillDoctorResult(
            HostKind.Codex,
            AbsolutePath.Parse(Path.GetFullPath("doctor-report-context-mismatch")),
            []);

        Assert.Throws<ArgumentException>(() => SkillOperationReportBuilder.CreateDoctorReport(
            result,
            new SkillOperationReportContext(
                ResolveHost(HostKind.ClaudeCode),
                SkillScopeKind.Project,
                Path.GetFullPath("."),
                [new SkillCategory("basic")],
                [])));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CreateInstallReport_RejectsMismatchedScope ()
    {
        var targetRoot = Path.GetFullPath("install-report-scope-mismatch");
        var result = new SkillInstallResult(
            AbsolutePath.Parse(targetRoot),
            [CreateNoOpInstallAction(CreateIdentity(targetRoot, "skill-a", SkillScopeKind.Project))],
            dryRun: false,
            force: false,
            printDiff: false);

        Assert.Throws<ArgumentException>(() => SkillOperationReportBuilder.CreateInstallReport(
            result,
            CreateContext(SkillScopeKind.User)));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void SkillInstallResult_RejectsMismatchedTargetRoot ()
    {
        var targetRoot = Path.GetFullPath("install-report-target-root-mismatch");
        Assert.Throws<ArgumentException>(() => new SkillInstallResult(
            AbsolutePath.Parse(Path.GetFullPath("install-report-other-target-root")),
            [CreateNoOpInstallAction(CreateIdentity(targetRoot, "skill-a"))],
            dryRun: false,
            force: false,
            printDiff: false));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void SkillActionTargetState_RejectsUnsupportedKind ()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SkillActionTargetState(
            (SkillTargetStateKind)999,
            code: null,
            message: null,
            fileSet: null,
            installedSkillBundleVersion: null,
            bundledSkillBundleVersion: null));
    }

    private static SkillInstallAction CreateNoOpInstallAction (SkillInstallIdentity identity)
    {
        return new SkillInstallAction(
            identity,
            SkillInstallActionKind.NoOp,
            CreateCurrentTargetState(),
            blockedReason: null,
            diffs: null,
            fileChanges: null);
    }

    private static SkillActionTargetState CreateMissingTargetState ()
    {
        return new SkillActionTargetState(
            SkillTargetStateKind.Missing,
            code: null,
            message: null,
            fileSet: null,
            installedSkillBundleVersion: null,
            bundledSkillBundleVersion: 1);
    }

    private static SkillActionTargetState CreateCurrentTargetState ()
    {
        return new SkillActionTargetState(
            SkillTargetStateKind.Current,
            code: null,
            message: null,
            fileSet: null,
            installedSkillBundleVersion: 1,
            bundledSkillBundleVersion: 1);
    }

    private static SkillActionTargetState CreateUnmanagedTargetState ()
    {
        return new SkillActionTargetState(
            SkillTargetStateKind.Unmanaged,
            AgentDistributionFailureCodes.InstallTargetUnmanaged,
            "Unmanaged.",
            fileSet: null,
            installedSkillBundleVersion: null,
            bundledSkillBundleVersion: null);
    }

    private static SkillInstallIdentity CreateIdentity (
        string targetRoot,
        string skillName,
        SkillScopeKind scope = SkillScopeKind.Project)
    {
        return new SkillInstallIdentity(
            HostKind.Codex,
            scope,
            AbsolutePath.Parse(targetRoot),
            new SkillName(skillName));
    }

    private static SkillResolvedHost CodexHost => ResolveHost(HostKind.Codex);

    private static IReadOnlyList<SkillResolvedHost> SupportedHosts => SkillTestData.CreateInstallTargetResolver().GetSupportedHosts();

    private static SkillResolvedHost ResolveHost (HostKind host)
    {
        var result = SkillTestData.CreateInstallTargetResolver().ResolveHost(host);
        Assert.True(result.IsSuccess, result.Failure?.Message);
        return result.Value!;
    }

    private static SkillOperationReportContext CreateContext ()
    {
        return CreateContext([new SkillCategory("basic")]);
    }

    private static SkillOperationReportContext CreateContext (SkillScopeKind scope)
    {
        return CreateContext(scope, [new SkillCategory("basic")]);
    }

    private static SkillOperationReportContext CreateContext (IReadOnlyList<SkillCategory> categories)
    {
        return CreateContext(SkillScopeKind.Project, categories);
    }

    private static SkillOperationReportContext CreateContext (
        IReadOnlyList<SkillCategory> categories,
        IReadOnlyList<SkillName> skillNames)
    {
        return new SkillOperationReportContext(
            CodexHost,
            SkillScopeKind.Project,
            Path.GetFullPath("."),
            categories,
            skillNames);
    }

    private static SkillOperationReportContext CreateContext (
        SkillScopeKind scope,
        IReadOnlyList<SkillCategory> categories)
    {
        return new SkillOperationReportContext(
            CodexHost,
            scope,
            scope == SkillScopeKind.Project ? Path.GetFullPath(".") : null,
            categories,
            []);
    }

    private static void AssertCount (
        IReadOnlyList<OperationCountReport> counts,
        string literal,
        int expected)
    {
        Assert.Equal(expected, counts.Single(count => string.Equals(count.Literal, literal, StringComparison.Ordinal)).Count);
    }

    private static Type[] GetPublicReportContractTypes ()
    {
        return typeof(SkillOperationReport).Assembly.GetTypes()
            .Where(static type =>
                type.IsPublic
                && string.Equals(type.Namespace, "MackySoft.AgentDistribution.OperationReports.Contracts", StringComparison.Ordinal)
                && type.Name.EndsWith("Report", StringComparison.Ordinal))
            .OrderBy(static type => type.Name, StringComparer.Ordinal)
            .ToArray();
    }

    private static IEnumerable<Type> GetUnsupportedPropertyTypes (Type type)
    {
        if (type.IsArray)
        {
            return GetUnsupportedPropertyTypes(type.GetElementType()!);
        }

        if (type.IsGenericType)
        {
            return type
                .GetGenericArguments()
                .SelectMany(GetUnsupportedPropertyTypes);
        }

        if (type.IsPrimitive
            || type == typeof(string)
            || type == typeof(Sha256Digest)
            || type.Namespace == "MackySoft.AgentDistribution.OperationReports.Contracts"
            || IsSupportedReportLiteralType(type))
        {
            return [];
        }

        return [type];
    }

    private static bool IsSupportedReportLiteralType (Type type)
    {
        return type == typeof(HostKind)
            || type == typeof(SkillScopeKind)
            || type == typeof(PackageExportFormat)
            || type == typeof(SkillDoctorSeverity)
            || type == typeof(OperationActionStatus)
            || type == typeof(OperationScopeKind)
            || type == typeof(AgentOperationTargetState)
            || type == typeof(AgentDiagnosticArea)
            || type == typeof(SkillBlockedReason)
            || type == typeof(SkillTargetStateKind)
            || type == typeof(SkillDiffChangeKind);
    }

}
