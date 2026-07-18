using MackySoft.AgentSkills.Categories;
using MackySoft.AgentSkills.Digests;
using MackySoft.AgentSkills.Distribution;
using MackySoft.AgentSkills.Doctor;
using MackySoft.AgentSkills.Hosts.Contracts;
using MackySoft.AgentSkills.Installation.Results;
using MackySoft.AgentSkills.Installation.Targeting;
using MackySoft.AgentSkills.Manifests;
using MackySoft.AgentSkills.Names;
using MackySoft.AgentSkills.OperationReports.Contracts;
using MackySoft.AgentSkills.OperationReports.Literals;
using MackySoft.AgentSkills.OperationReports.Projection;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Tests.OperationReports;

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
            targetRoot,
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
                        SkillFailureCodes.InstallTargetFileSetMismatch,
                        "File set drift.",
                        new SkillActionTargetFileSet(["missing.md"], ["extra.md"], ["extra-dir"]),
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
                        SkillFailureCodes.InstallTargetContentDigestMismatch,
                        "Content drift.",
                        fileSet: null,
                        installedSkillBundleVersion: null,
                        bundledSkillBundleVersion: 2),
                    blockedReason: null,
                    diffs:
                    [
                        new SkillActionDiff(
                        [
                            new SkillFileDiff("z.txt", SkillDiffChangeKind.Deleted, "old", null),
                            new SkillFileDiff("a.txt", SkillDiffChangeKind.Added, null, "new"),
                        ]),
                    ],
                    fileChanges: new SkillActionFileChanges(["z.txt", "a.txt"], ["local.md"])),
            ],
            dryRun: true,
            force: true,
            printDiff: true);

        var report = SkillOperationReportBuilder.CreateInstallReport(result, context);

        Assert.Equal(SkillHostKind.OpenAi, report.Host);
        Assert.Equal(["basic", "advanced"], report.Categories);
        Assert.Equal(["skill-a", "skill-c"], report.SkillNames);
        Assert.Equal(SkillScopeKind.Project, report.Scope);
        Assert.Equal(targetRoot, report.TargetRoot);
        Assert.True(report.DryRun);
        Assert.True(report.Force);
        Assert.Equal(OpenAiDescriptor.ReloadGuidance, report.ReloadGuidance);
        Assert.Equal(["skill-a", "skill-b", "skill-c"], report.Actions.Select(static action => action.SkillName).ToArray());

        var updated = report.Actions[0];
        Assert.Equal("updated", updated.Action);
        Assert.Equal(SkillOperationActionStatus.Changed, updated.Status);
        Assert.Null(updated.BlockedReason);
        Assert.Equal(SkillTargetStateKind.CommonContentDrift, updated.TargetState!.Kind);
        Assert.Equal(SkillFailureCodes.InstallTargetContentDigestMismatch.Value, updated.TargetState.Code);
        Assert.Null(updated.TargetState.InstalledSkillBundleVersion);
        Assert.Equal(2, updated.TargetState.BundledSkillBundleVersion);
        Assert.Equal(["a.txt", "z.txt"], updated.FileChanges!.ReplacedFiles);
        Assert.Equal(["local.md"], updated.FileChanges.RemovedFiles);
        Assert.Equal(["a.txt", "z.txt"], updated.FileDiffs.Select(static diff => diff.RelativePath).ToArray());
        Assert.Equal(SkillDiffChangeKind.Added, updated.FileDiffs[0].ChangeKind);
        Assert.Equal(SkillDiffChangeKind.Deleted, updated.FileDiffs[1].ChangeKind);

        var blocked = report.Actions[2];
        Assert.Equal("blockedLocalModification", blocked.Action);
        Assert.Equal(SkillOperationActionStatus.Blocked, blocked.Status);
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
            targetRoot,
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
                            new SkillFileDiff("SKILL.md", SkillDiffChangeKind.Modified, "old", "new"),
                        ]),
                    ],
                    fileChanges: new SkillActionFileChanges([], [])),
            ],
            dryRun: true,
            force: false,
            printDiff: false);

        var report = SkillOperationReportBuilder.CreateUpdateReport(result, context);

        Assert.Equal(SkillScopeKind.User, report.Scope);
        Assert.Equal(["skill-a", "skill-b"], report.Actions.Select(static action => action.SkillName).ToArray());
        Assert.Equal("created", report.Actions[0].Action);
        Assert.Equal(SkillOperationActionStatus.Changed, report.Actions[0].Status);
        Assert.Empty(report.Actions[0].FileDiffs);
        Assert.Equal("blockedUnmanaged", report.Actions[1].Action);
        Assert.Equal(SkillOperationActionStatus.Blocked, report.Actions[1].Status);
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
            targetRoot,
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
                    fileChanges: new SkillActionFileChanges([], ["SKILL.md", "agent-skill.json"])),
            ],
            dryRun: false,
            force: true);

        var report = SkillOperationReportBuilder.CreateUninstallReport(result, context);

        Assert.Equal("deleted", report.Actions[0].Action);
        Assert.Equal(SkillOperationActionStatus.Changed, report.Actions[0].Status);
        Assert.Equal(["SKILL.md", "agent-skill.json"], report.Actions[0].FileChanges!.RemovedFiles);
        Assert.Equal("skippedUnmanaged", report.Actions[1].Action);
        Assert.Equal(SkillOperationActionStatus.Skipped, report.Actions[1].Status);
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
            targetRoot,
            [
                new SkillPruneAction(
                    CreateIdentity(targetRoot, "skill-a"),
                    SkillPruneActionKind.Deleted,
                    new SkillActionTargetState(
                        SkillTargetStateKind.RemovedFromCatalog,
                        SkillFailureCodes.InstallTargetRemovedFromCatalog,
                        "Removed from catalog.",
                        fileSet: null,
                        installedSkillBundleVersion: 1,
                        bundledSkillBundleVersion: null),
                    blockedReason: null,
                    fileChanges: new SkillActionFileChanges([], ["SKILL.md", "agent-skill.json"])),
                new SkillPruneAction(CreateIdentity(targetRoot, "skill-b"), SkillPruneActionKind.SkippedCurrent, null, null, null),
                new SkillPruneAction(CreateIdentity(targetRoot, "skill-c"), SkillPruneActionKind.SkippedForeignCatalog, null, null, null),
                new SkillPruneAction(
                    CreateIdentity(targetRoot, "skill-d"),
                    SkillPruneActionKind.SkippedUnmanaged,
                    new SkillActionTargetState(
                        SkillTargetStateKind.Unmanaged,
                        SkillFailureCodes.InstallTargetUnmanaged,
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
                        SkillFailureCodes.InstallTargetContentDigestMismatch,
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
                        SkillFailureCodes.ManifestInvalid,
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
                        SkillFailureCodes.InstallTargetNameCollision,
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
                        SkillFailureCodes.InstallTargetHostConflict,
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
                SkillOperationActionStatus.Changed,
                SkillOperationActionStatus.NoOp,
                SkillOperationActionStatus.Skipped,
                SkillOperationActionStatus.Skipped,
                SkillOperationActionStatus.Blocked,
                SkillOperationActionStatus.Blocked,
                SkillOperationActionStatus.Blocked,
                SkillOperationActionStatus.Blocked,
            ],
            report.Actions.Select(static action => action.Status).ToArray());
        var deletedTargetState = report.Actions[0].TargetState!;
        Assert.Equal(SkillTargetStateKind.RemovedFromCatalog, deletedTargetState.Kind);
        Assert.Equal(SkillFailureCodes.InstallTargetRemovedFromCatalog.Value, deletedTargetState.Code);
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
        var hostAdapters = SkillTestData.CreateDefaultHostAdapterSet();
        var catalog = new SkillPackageCatalog(
            bundle.Descriptor,
            [new SkillCategory("core")],
            [packages[0].Manifest.SkillName],
            [new SkillCategoryPackageCount(new SkillCategory("core"), packages.Length)],
            packages);

        var report = SkillOperationReportBuilder.CreateListReport(
            catalog,
            hostAdapters);

        Assert.Equal(["core"], report.Categories);
        Assert.Equal([packages[0].Manifest.SkillName.Value], report.SkillNames);
        Assert.Equal(["core"], report.AvailableCategories.Select(static category => category.Category).ToArray());
        Assert.Equal([packages.Length], report.AvailableCategories.Select(static category => category.SkillCount).ToArray());
        Assert.Equal(SkillTestData.ExpectedSkillNames, report.Skills.Select(static skill => skill.SkillName).ToArray());
        Assert.All(report.Skills, static skill => Assert.Empty(skill.Dependencies));
        Assert.All(report.Skills, static skill => Assert.Equal("core", skill.Category));
        Assert.All(report.Skills, static skill => Assert.Equal(1, skill.SkillBundleVersion));
        Assert.All(report.Skills, static skill => Assert.Equal("com.mackysoft.agent-skills", skill.CatalogId));
        Assert.Equal([SkillHostKind.Claude, SkillHostKind.Copilot, SkillHostKind.OpenAi], report.SupportedHosts.Select(static host => host.Host).ToArray());
        var openAi = report.SupportedHosts.Single(static host => host.Host == SkillHostKind.OpenAi);
        Assert.Equal("catalog-directory", openAi.BundleTargetRootLayout);
        Assert.Equal(["flat"], openAi.CompatiblePreviousBundleTargetRootLayouts);
        Assert.Equal("agents/openai.yaml", openAi.MetadataArtifactPath);
        Assert.Equal(
            [SkillHostKind.Claude, SkillHostKind.Copilot, SkillHostKind.OpenAi],
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
            .Select(file => string.Equals(file.RelativePath, "agent-skill.json", StringComparison.Ordinal)
                ? new SkillPackageFile(file.RelativePath, serializer.Serialize(manifest))
                : file)
            .ToArray();
        packages[0] = SkillTestData.CreateCanonicalPackage(manifest, files);
        var catalog = new SkillPackageCatalog(
            bundle.Descriptor,
            [new SkillCategory("core")],
            [],
            [new SkillCategoryPackageCount(new SkillCategory("core"), packages.Length)],
            packages);

        var report = SkillOperationReportBuilder.CreateListReport(
            catalog,
            SkillTestData.CreateDefaultHostAdapterSet());

        var skill = report.Skills.Single(skill => string.Equals(skill.SkillName, packages[0].Manifest.SkillName.Value, StringComparison.Ordinal));
        Assert.Equal([packages[1].Manifest.SkillName.Value], skill.Dependencies);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task CreateExportReport_ProjectsFormatAndSortedSkillNames ()
    {
        var packages = (await SkillTestData.GenerateFixturePackagesAsync()).Reverse().ToArray();
        var outputPath = Path.Combine("tmp", "agent-skills.zip");

        var report = SkillOperationReportBuilder.CreateExportReport(
            outputPath,
            packages,
            OpenAiDescriptor,
            SkillExportFormat.Zip,
            [new SkillCategory("basic"), new SkillCategory("advanced")],
            [packages[0].Manifest.SkillName]);

        Assert.Equal(SkillHostKind.OpenAi, report.Host);
        Assert.Equal(["basic", "advanced"], report.Categories);
        Assert.Equal([packages[0].Manifest.SkillName.Value], report.SkillNames);
        Assert.Equal(SkillExportFormat.Zip, report.Format);
        Assert.Equal(Path.GetFullPath(outputPath), report.OutputPath);
        Assert.Equal(SkillTestData.ExpectedSkillNames, report.Skills);
        Assert.Equal(SkillTestData.ExpectedSkillNames.Length, report.SkillCount);
        Assert.Equal(OpenAiDescriptor.ReloadGuidance, report.ReloadGuidance);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CreateDoctorReport_ProjectsSeverityAndTargetStateFromDiagnostics ()
    {
        var targetRoot = Path.GetFullPath("agent-skills-doctor");
        var result = new SkillDoctorResult(
            SkillHostKind.OpenAi,
            targetRoot,
            [
                SkillDoctorDiagnostic.Error(
                    SkillFailureCodes.InstallTargetHostArtifactDigestMismatch,
                    "Host artifact drift.",
                    "skill-b"),
                SkillDoctorDiagnostic.Error(
                    SkillFailureCodes.InstallTargetUnmanaged,
                    "Target root is missing."),
                SkillDoctorDiagnostic.Error(
                    SkillFailureCodes.InstallTargetVersionAhead,
                    "Version ahead.",
                    "skill-c"),
                SkillDoctorDiagnostic.Error(
                    SkillFailureCodes.InstallTargetOutdated,
                    "Clean outdated.",
                    "skill-d"),
                SkillDoctorDiagnostic.Error(
                    SkillFailureCodes.InstallTargetRemovedFromCatalog,
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
                OpenAiDescriptor,
                SkillScopeKind.Project,
                Path.GetFullPath("."),
                [new SkillCategory("developer")],
                [new SkillName("skill-a")]));

        Assert.False(report.IsHealthy);
        Assert.Equal(["developer"], report.Categories);
        Assert.Equal(["skill-a"], report.SkillNames);
        Assert.Equal(SkillScopeKind.Project, report.Scope);
        Assert.Equal(Path.GetFullPath("."), report.RepositoryRoot);
        Assert.Equal(OpenAiDescriptor.ReloadGuidance, report.ReloadGuidance);
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
    public void OperationReportsPublicSurface_ContainsOnlyExpectedTypes ()
    {
        var expectedTypeNames = new[]
        {
            "MackySoft.AgentSkills.OperationReports.Contracts.SkillDoctorDiagnosticReport",
            "MackySoft.AgentSkills.OperationReports.Contracts.SkillDoctorReport",
            "MackySoft.AgentSkills.OperationReports.Contracts.SkillExportReport",
            "MackySoft.AgentSkills.OperationReports.Contracts.SkillHostArtifactReport",
            "MackySoft.AgentSkills.OperationReports.Contracts.SkillHostReport",
            "MackySoft.AgentSkills.OperationReports.Contracts.SkillListReport",
            "MackySoft.AgentSkills.OperationReports.Contracts.SkillListSkillReport",
            "MackySoft.AgentSkills.OperationReports.Contracts.SkillListCategoryReport",
            "MackySoft.AgentSkills.OperationReports.Contracts.SkillOperationActionReport",
            "MackySoft.AgentSkills.OperationReports.Contracts.SkillOperationCountReport",
            "MackySoft.AgentSkills.OperationReports.Contracts.SkillOperationFileChangesReport",
            "MackySoft.AgentSkills.OperationReports.Contracts.SkillOperationFileDiffReport",
            "MackySoft.AgentSkills.OperationReports.Contracts.SkillOperationReport",
            "MackySoft.AgentSkills.OperationReports.Contracts.SkillTargetFileSetReport",
            "MackySoft.AgentSkills.OperationReports.Contracts.SkillTargetStateReport",
            "MackySoft.AgentSkills.OperationReports.Contracts.SkillUserTargetRootPolicyReport",
            "MackySoft.AgentSkills.OperationReports.Literals.SkillOperationActionStatus",
            "MackySoft.AgentSkills.OperationReports.Projection.SkillOperationReportBuilder",
            "MackySoft.AgentSkills.OperationReports.Projection.SkillOperationReportContext",
        };
        var actualTypeNames = typeof(SkillOperationReport).Assembly.GetTypes()
            .Where(static type => type.IsPublic && type.Namespace?.StartsWith("MackySoft.AgentSkills.OperationReports", StringComparison.Ordinal) == true)
            .Select(static type => type.FullName!)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expectedTypeNames.Order(StringComparer.Ordinal), actualTypeNames);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void OperationReportPublicContracts_ExposeOnlyExpectedProperties ()
    {
        AssertProperties<SkillDoctorDiagnosticReport>(
            ("Severity", typeof(SkillDoctorSeverity)),
            ("Code", typeof(string)),
            ("Message", typeof(string)),
            ("SkillName", typeof(string)),
            ("TargetState", typeof(SkillTargetStateKind?)));
        AssertProperties<SkillDoctorReport>(
            ("Host", typeof(SkillHostKind)),
            ("Categories", typeof(IReadOnlyList<string>)),
            ("SkillNames", typeof(IReadOnlyList<string>)),
            ("Scope", typeof(SkillScopeKind)),
            ("RepositoryRoot", typeof(string)),
            ("TargetRoot", typeof(string)),
            ("ReloadGuidance", typeof(string)),
            ("IsHealthy", typeof(bool)),
            ("Diagnostics", typeof(IReadOnlyList<SkillDoctorDiagnosticReport>)));
        AssertProperties<SkillExportReport>(
            ("Host", typeof(SkillHostKind)),
            ("Categories", typeof(IReadOnlyList<string>)),
            ("SkillNames", typeof(IReadOnlyList<string>)),
            ("Format", typeof(SkillExportFormat)),
            ("OutputPath", typeof(string)),
            ("Skills", typeof(IReadOnlyList<string>)),
            ("SkillCount", typeof(int)),
            ("ReloadGuidance", typeof(string)));
        AssertProperties<SkillHostArtifactReport>(
            ("Host", typeof(SkillHostKind)),
            ("Path", typeof(string)),
            ("Digest", typeof(Sha256Digest)),
            ("MaterializedFrontmatterDigest", typeof(Sha256Digest)));
        AssertProperties<SkillHostReport>(
            ("Host", typeof(SkillHostKind)),
            ("ProjectDefaultTargetPath", typeof(string)),
            ("UserDefaultTargetPath", typeof(string)),
            ("UserTargetRootPolicy", typeof(SkillUserTargetRootPolicyReport)),
            ("BundleTargetRootLayout", typeof(string)),
            ("CompatiblePreviousBundleTargetRootLayouts", typeof(IReadOnlyList<string>)),
            ("MetadataArtifactPath", typeof(string)),
            ("ReloadGuidance", typeof(string)));
        AssertProperties<SkillListReport>(
            ("Categories", typeof(IReadOnlyList<string>)),
            ("SkillNames", typeof(IReadOnlyList<string>)),
            ("AvailableCategories", typeof(IReadOnlyList<SkillListCategoryReport>)),
            ("Skills", typeof(IReadOnlyList<SkillListSkillReport>)),
            ("SupportedHosts", typeof(IReadOnlyList<SkillHostReport>)));
        AssertProperties<SkillListSkillReport>(
            ("SchemaVersion", typeof(int)),
            ("SkillBundleVersion", typeof(int)),
            ("SkillName", typeof(string)),
            ("DisplayName", typeof(string)),
            ("Description", typeof(string)),
            ("Dependencies", typeof(IReadOnlyList<string>)),
            ("Category", typeof(string)),
            ("CatalogId", typeof(string)),
            ("ContentDigest", typeof(Sha256Digest)),
            ("ManifestDigest", typeof(Sha256Digest)),
            ("HostArtifacts", typeof(IReadOnlyList<SkillHostArtifactReport>)));
        AssertProperties<SkillListCategoryReport>(
            ("Category", typeof(string)),
            ("SkillCount", typeof(int)));
        AssertProperties<SkillOperationActionReport>(
            ("SkillName", typeof(string)),
            ("Action", typeof(string)),
            ("Status", typeof(SkillOperationActionStatus)),
            ("BlockedReason", typeof(SkillBlockedReason?)),
            ("TargetState", typeof(SkillTargetStateReport)),
            ("FileChanges", typeof(SkillOperationFileChangesReport)),
            ("FileDiffs", typeof(IReadOnlyList<SkillOperationFileDiffReport>)));
        AssertProperties<SkillOperationCountReport>(
            ("Literal", typeof(string)),
            ("Count", typeof(int)));
        AssertProperties<SkillOperationFileChangesReport>(
            ("ReplacedFiles", typeof(IReadOnlyList<string>)),
            ("RemovedFiles", typeof(IReadOnlyList<string>)));
        AssertProperties<SkillOperationFileDiffReport>(
            ("RelativePath", typeof(string)),
            ("ChangeKind", typeof(SkillDiffChangeKind)),
            ("BeforeContent", typeof(string)),
            ("AfterContent", typeof(string)));
        AssertProperties<SkillOperationReport>(
            ("Host", typeof(SkillHostKind)),
            ("Categories", typeof(IReadOnlyList<string>)),
            ("SkillNames", typeof(IReadOnlyList<string>)),
            ("Scope", typeof(SkillScopeKind)),
            ("RepositoryRoot", typeof(string)),
            ("TargetRoot", typeof(string)),
            ("DryRun", typeof(bool)),
            ("Force", typeof(bool)),
            ("ReloadGuidance", typeof(string)),
            ("Actions", typeof(IReadOnlyList<SkillOperationActionReport>)),
            ("ActionCounts", typeof(IReadOnlyList<SkillOperationCountReport>)),
            ("StatusCounts", typeof(IReadOnlyList<SkillOperationCountReport>)));
        AssertProperties<SkillTargetFileSetReport>(
            ("MissingFiles", typeof(IReadOnlyList<string>)),
            ("ExtraFiles", typeof(IReadOnlyList<string>)),
            ("ExtraDirectories", typeof(IReadOnlyList<string>)));
        AssertProperties<SkillTargetStateReport>(
            ("Kind", typeof(SkillTargetStateKind)),
            ("Code", typeof(string)),
            ("Message", typeof(string)),
            ("InstalledSkillBundleVersion", typeof(int?)),
            ("BundledSkillBundleVersion", typeof(int?)),
            ("FileSet", typeof(SkillTargetFileSetReport)));
        AssertProperties<SkillUserTargetRootPolicyReport>(
            ("EnvironmentVariableName", typeof(string)),
            ("EnvironmentVariableChildDirectory", typeof(string)),
            ("HomeRelativeDirectory", typeof(string)));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("../escape.md")]
    [InlineData("/tmp/escape.md")]
    [InlineData("unsafe\\name.md")]
    [InlineData("C:/escape.md")]
    [InlineData("skill:C.md")]
    [InlineData("unsafe\u001fname.md")]
    public void SkillActionFileChanges_RejectsUnsafePath (string unsafePath)
    {
        Assert.Throws<ArgumentException>(() => new SkillActionFileChanges([unsafePath], []));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("../escape.md")]
    [InlineData("/tmp/escape.md")]
    [InlineData("unsafe\\name.md")]
    [InlineData("C:/escape.md")]
    [InlineData("skill:C.md")]
    [InlineData("unsafe\u001fname.md")]
    public void SkillFileDiff_RejectsUnsafePath (string unsafePath)
    {
        Assert.Throws<ArgumentException>(() => new SkillFileDiff(unsafePath, SkillDiffChangeKind.Modified, "old", "new"));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("../escape.md")]
    [InlineData("/tmp/escape.md")]
    [InlineData("unsafe\\name.md")]
    [InlineData("C:/escape.md")]
    [InlineData("skill:C.md")]
    [InlineData("unsafe\u001fname.md")]
    public void SkillActionTargetFileSet_RejectsUnsafePath (string unsafePath)
    {
        Assert.Throws<ArgumentException>(() => new SkillActionTargetFileSet([unsafePath], [], []));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CreateInstallReport_RejectsMismatchedContext ()
    {
        var targetRoot = Path.GetFullPath("install-report-context-mismatch");
        var result = new SkillInstallResult(
            targetRoot,
            [CreateNoOpInstallAction(CreateIdentity(targetRoot, "skill-a"))],
            dryRun: false,
            force: false,
            printDiff: false);

        Assert.Throws<ArgumentException>(() => SkillOperationReportBuilder.CreateInstallReport(
            result,
            new SkillOperationReportContext(
                GetHostDescriptor(SkillHostKind.Claude),
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
            SkillHostKind.OpenAi,
            Path.GetFullPath("doctor-report-context-mismatch"),
            []);

        Assert.Throws<ArgumentException>(() => SkillOperationReportBuilder.CreateDoctorReport(
            result,
            new SkillOperationReportContext(
                GetHostDescriptor(SkillHostKind.Claude),
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
            targetRoot,
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
            Path.GetFullPath("install-report-other-target-root"),
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
            SkillFailureCodes.InstallTargetUnmanaged,
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
            SkillHostKind.OpenAi,
            scope,
            targetRoot,
            new SkillName(skillName));
    }

    private static SkillHostDescriptor OpenAiDescriptor => GetHostDescriptor(SkillHostKind.OpenAi);

    private static SkillHostDescriptor GetHostDescriptor (SkillHostKind host)
    {
        var result = SkillTestData.CreateDefaultHostAdapterSet().GetAdapter(host);
        Assert.True(result.IsSuccess, result.Failure?.Message);
        return result.Value!.Descriptor;
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
            OpenAiDescriptor,
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
            OpenAiDescriptor,
            scope,
            scope == SkillScopeKind.Project ? Path.GetFullPath(".") : null,
            categories,
            []);
    }

    private static void AssertCount (
        IReadOnlyList<SkillOperationCountReport> counts,
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
                && string.Equals(type.Namespace, "MackySoft.AgentSkills.OperationReports.Contracts", StringComparison.Ordinal)
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
            || type.Namespace == "MackySoft.AgentSkills.OperationReports.Contracts"
            || IsSupportedReportLiteralType(type))
        {
            return [];
        }

        return [type];
    }

    private static bool IsSupportedReportLiteralType (Type type)
    {
        return type == typeof(SkillHostKind)
            || type == typeof(SkillScopeKind)
            || type == typeof(SkillExportFormat)
            || type == typeof(SkillDoctorSeverity)
            || type == typeof(SkillOperationActionStatus)
            || type == typeof(SkillBlockedReason)
            || type == typeof(SkillTargetStateKind)
            || type == typeof(SkillDiffChangeKind);
    }

    private static void AssertProperties<T> (params (string Name, Type Type)[] expectedProperties)
    {
        var actualProperties = typeof(T)
            .GetProperties()
            .Select(static property => (property.Name, property.PropertyType))
            .ToArray();

        Assert.Equal(expectedProperties, actualProperties);
    }
}
