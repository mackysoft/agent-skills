using MackySoft.AgentSkills.Distribution;
using MackySoft.AgentSkills.Doctor;
using MackySoft.AgentSkills.Hosts.Claude;
using MackySoft.AgentSkills.Hosts.Contracts;
using MackySoft.AgentSkills.Hosts.OpenAi;
using MackySoft.AgentSkills.Installation.Results;
using MackySoft.AgentSkills.Installation.State;
using MackySoft.AgentSkills.Installation.Targeting;
using MackySoft.AgentSkills.OperationReports.Contracts;
using MackySoft.AgentSkills.OperationReports.Projection;
using MackySoft.AgentSkills.Shared;
using MackySoft.AgentSkills.Tiers;

namespace MackySoft.AgentSkills.Tests.OperationReports;

public sealed class SkillOperationReportBuilderTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void CreateInstallReport_ProjectsActionsCountsAndFileDetails ()
    {
        var targetRoot = Path.GetFullPath("install-report-target");
        var context = CreateContext([new SkillTier("basic"), new SkillTier("advanced")]);
        var result = new SkillInstallResult(
            targetRoot,
            [
                new SkillInstallAction(
                    CreateIdentity(targetRoot, "skill-b"),
                    SkillInstallActionKind.NoOp,
                    TargetState: new SkillActionTargetState(nameof(SkillInstalledTargetStateKind.Current))),
                new SkillInstallAction(
                    CreateIdentity(targetRoot, "skill-c"),
                    SkillInstallActionKind.BlockedLocalModification,
                    SkillBlockedReason.LocalModificationRequiresForce,
                    TargetState: new SkillActionTargetState(
                        nameof(SkillInstalledTargetStateKind.FileSetDrift),
                        SkillFailureCodes.InstallTargetFileSetMismatch,
                        "File set drift.",
                        new SkillActionTargetFileSet(["missing.md"], ["extra.md"], ["extra-dir"]))),
                new SkillInstallAction(
                    CreateIdentity(targetRoot, "skill-a"),
                    SkillInstallActionKind.Updated,
                    Diffs:
                    [
                        new SkillActionDiff(
                        [
                            new SkillFileDiff("z.txt", SkillDiffChangeKind.Deleted, "old", null),
                            new SkillFileDiff("a.txt", SkillDiffChangeKind.Added, null, "new"),
                        ]),
                    ],
                    TargetState: new SkillActionTargetState(
                        nameof(SkillInstalledTargetStateKind.CommonContentDrift),
                        SkillFailureCodes.InstallTargetContentDigestMismatch,
                        "Content drift."))
                {
                    FileChanges = new SkillActionFileChanges(["z.txt", "a.txt"], ["local.md"]),
                },
            ],
            DryRun: true,
            Force: true,
            PrintDiff: true);

        var report = SkillOperationReportBuilder.CreateInstallReport(result, context);

        Assert.Equal(OpenAiSkillHostAdapter.HostKey, report.Host);
        Assert.Equal(["basic", "advanced"], report.Tiers);
        Assert.Equal("project", report.Scope);
        Assert.Equal(targetRoot, report.TargetRoot);
        Assert.True(report.DryRun);
        Assert.True(report.Force);
        Assert.Equal(OpenAiDescriptor.ReloadGuidance, report.ReloadGuidance);
        Assert.Equal(["skill-a", "skill-b", "skill-c"], report.Actions.Select(static action => action.SkillName).ToArray());

        var updated = report.Actions[0];
        Assert.Equal("updated", updated.Action);
        Assert.Equal("changed", updated.Status);
        Assert.Null(updated.BlockedReason);
        Assert.Equal("commonContentDrift", updated.TargetState!.Kind);
        Assert.Equal(SkillFailureCodes.InstallTargetContentDigestMismatch.Value, updated.TargetState.Code);
        Assert.Equal(["a.txt", "z.txt"], updated.FileChanges!.ReplacedFiles);
        Assert.Equal(["local.md"], updated.FileChanges.RemovedFiles);
        Assert.Equal(["a.txt", "z.txt"], updated.FileDiffs.Select(static diff => diff.RelativePath).ToArray());
        Assert.Equal("added", updated.FileDiffs[0].ChangeKind);
        Assert.Equal("deleted", updated.FileDiffs[1].ChangeKind);

        var blocked = report.Actions[2];
        Assert.Equal("blockedLocalModification", blocked.Action);
        Assert.Equal("blocked", blocked.Status);
        Assert.Equal("localModificationRequiresForce", blocked.BlockedReason);
        Assert.Equal("fileSetDrift", blocked.TargetState!.Kind);
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
                new SkillUpdateAction(CreateIdentity(targetRoot, "skill-b", SkillScopeKind.User), SkillUpdateActionKind.BlockedUnmanaged, SkillBlockedReason.UnmanagedTarget),
                new SkillUpdateAction(
                    CreateIdentity(targetRoot, "skill-a", SkillScopeKind.User),
                    SkillUpdateActionKind.Created,
                    Diffs:
                    [
                        new SkillActionDiff(
                        [
                            new SkillFileDiff("SKILL.md", SkillDiffChangeKind.Modified, "old", "new"),
                        ]),
                    ])
                {
                    FileChanges = new SkillActionFileChanges([], []),
                },
            ],
            DryRun: true,
            Force: false,
            PrintDiff: false);

        var report = SkillOperationReportBuilder.CreateUpdateReport(result, context);

        Assert.Equal("user", report.Scope);
        Assert.Equal(["skill-a", "skill-b"], report.Actions.Select(static action => action.SkillName).ToArray());
        Assert.Equal("created", report.Actions[0].Action);
        Assert.Equal("changed", report.Actions[0].Status);
        Assert.Empty(report.Actions[0].FileDiffs);
        Assert.Equal("blockedUnmanaged", report.Actions[1].Action);
        Assert.Equal("blocked", report.Actions[1].Status);
        Assert.Equal("unmanagedTarget", report.Actions[1].BlockedReason);
        Assert.Equal(
            ["created", "updated", "noOp", "blockedLocalModification", "blockedUnmanaged"],
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
                new SkillUninstallAction(CreateIdentity(targetRoot, "skill-b"), SkillUninstallActionKind.SkippedUnmanaged),
                new SkillUninstallAction(CreateIdentity(targetRoot, "skill-a"), SkillUninstallActionKind.Deleted)
                {
                    FileChanges = new SkillActionFileChanges([], ["SKILL.md", "agent-skill.json"]),
                },
            ],
            DryRun: false,
            Force: true);

        var report = SkillOperationReportBuilder.CreateUninstallReport(result, context);

        Assert.Equal("deleted", report.Actions[0].Action);
        Assert.Equal("changed", report.Actions[0].Status);
        Assert.Equal(["SKILL.md", "agent-skill.json"], report.Actions[0].FileChanges!.RemovedFiles);
        Assert.Equal("skippedUnmanaged", report.Actions[1].Action);
        Assert.Equal("skipped", report.Actions[1].Status);
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
    public async Task CreateListReport_UsesCanonicalSkillAndHostDescriptorData ()
    {
        var packages = (await SkillTestData.GenerateFixturePackagesAsync()).Reverse().ToArray();
        var hostAdapters = SkillTestData.CreateDefaultHostAdapterSet();
        var catalog = new SkillPackageCatalog(
            [new SkillTier("basic")],
            [
                new SkillTierPackageCount(new SkillTier("basic"), packages.Length),
                new SkillTierPackageCount(new SkillTier("advanced"), 0),
                new SkillTierPackageCount(new SkillTier("developer"), 0),
            ],
            packages);

        var report = SkillOperationReportBuilder.CreateListReport(
            catalog,
            hostAdapters);

        Assert.Equal(["basic"], report.Tiers);
        Assert.Equal(["basic", "advanced", "developer"], report.AvailableTiers.Select(static tier => tier.Tier).ToArray());
        Assert.Equal([packages.Length, 0, 0], report.AvailableTiers.Select(static tier => tier.SkillCount).ToArray());
        Assert.Equal(SkillTestData.ExpectedSkillNames, report.Skills.Select(static skill => skill.SkillName).ToArray());
        Assert.All(report.Skills, static skill => Assert.Equal("basic", skill.Tier));
        Assert.Equal(["claude", "copilot", "openai"], report.SupportedHosts.Select(static host => host.Host).ToArray());
        var openAi = report.SupportedHosts.Single(static host => host.Host == OpenAiSkillHostAdapter.HostKey);
        Assert.True(openAi.SupportsProjectScope);
        Assert.True(openAi.SupportsUserScope);
        Assert.True(openAi.RequiresMetadataArtifact);
        Assert.Equal("agents/openai.yaml", openAi.MetadataArtifactPath);
        Assert.Equal(
            ["claude", "copilot", "openai"],
            report.Skills[0].HostArtifacts.Select(static artifact => artifact.Host).ToArray());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task CreateExportReport_ProjectsFormatAndSortedSkillNames ()
    {
        var packages = (await SkillTestData.GenerateFixturePackagesAsync()).Reverse().ToArray();

        var report = SkillOperationReportBuilder.CreateExportReport(
            "/tmp/agent-skills.zip",
            packages,
            OpenAiDescriptor,
            SkillExportFormat.Zip,
            [new SkillTier("basic"), new SkillTier("advanced")]);

        Assert.Equal(OpenAiSkillHostAdapter.HostKey, report.Host);
        Assert.Equal(["basic", "advanced"], report.Tiers);
        Assert.Equal("zip", report.Format);
        Assert.Equal("/tmp/agent-skills.zip", report.OutputPath);
        Assert.Equal(SkillTestData.ExpectedSkillNames, report.Skills);
        Assert.Equal(SkillTestData.ExpectedSkillNames.Length, report.SkillCount);
        Assert.Equal(OpenAiDescriptor.ReloadGuidance, report.ReloadGuidance);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CreateDoctorReport_ProjectsSeverityAndTargetStateFromDiagnostics ()
    {
        var result = new SkillDoctorResult(
            OpenAiSkillHostAdapter.HostKey,
            "/tmp/agent-skills-doctor",
            [
                SkillDoctorDiagnostic.Error(
                    SkillFailureCodes.InstallTargetHostArtifactDigestMismatch,
                    "Host artifact drift.",
                    "skill-b"),
                SkillDoctorDiagnostic.Error(
                    SkillFailureCodes.InstallTargetUnmanaged,
                    "Target root is missing."),
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

        var report = SkillOperationReportBuilder.CreateDoctorReport(result, SkillScopeKind.Project, new SkillTier("developer"));

        Assert.False(report.IsHealthy);
        Assert.Equal(["developer"], report.Tiers);
        Assert.Equal("project", report.Scope);
        Assert.Equal(new string?[] { null, "skill-a", "skill-a", "skill-a", "skill-b" }, report.Diagnostics.Select(static diagnostic => diagnostic.SkillName).ToArray());
        Assert.Equal("error", report.Diagnostics[0].Severity);
        Assert.Null(report.Diagnostics[0].TargetState);
        Assert.Equal("info", report.Diagnostics[1].Severity);
        Assert.Null(report.Diagnostics[1].TargetState);
        Assert.Equal("info", report.Diagnostics[2].Severity);
        Assert.Equal("error", report.Diagnostics[3].Severity);
        Assert.Equal("hostArtifactDrift", report.Diagnostics[4].TargetState);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void OperationReportPublicContracts_DoNotExposeProductEnvelopeFields ()
    {
        var forbiddenTerms = new[] { "command", "exitCode", "repositoryRoot", "ucli", "dotmet" };
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
    public void OperationReportPublicContracts_DoNotExposeSourceModelTypes ()
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
            "MackySoft.AgentSkills.OperationReports.Contracts.SkillListTierReport",
            "MackySoft.AgentSkills.OperationReports.Contracts.SkillOperationActionReport",
            "MackySoft.AgentSkills.OperationReports.Contracts.SkillOperationCountReport",
            "MackySoft.AgentSkills.OperationReports.Contracts.SkillOperationFileChangesReport",
            "MackySoft.AgentSkills.OperationReports.Contracts.SkillOperationFileDiffReport",
            "MackySoft.AgentSkills.OperationReports.Contracts.SkillOperationReport",
            "MackySoft.AgentSkills.OperationReports.Contracts.SkillTargetFileSetReport",
            "MackySoft.AgentSkills.OperationReports.Contracts.SkillTargetStateReport",
            "MackySoft.AgentSkills.OperationReports.Contracts.SkillUserTargetRootPolicyReport",
            "MackySoft.AgentSkills.OperationReports.Literals.SkillLiteralCodec",
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
            ("Severity", typeof(string)),
            ("Code", typeof(string)),
            ("Message", typeof(string)),
            ("SkillName", typeof(string)),
            ("TargetState", typeof(string)));
        AssertProperties<SkillDoctorReport>(
            ("Host", typeof(string)),
            ("Tiers", typeof(IReadOnlyList<string>)),
            ("Scope", typeof(string)),
            ("TargetRoot", typeof(string)),
            ("IsHealthy", typeof(bool)),
            ("Diagnostics", typeof(IReadOnlyList<SkillDoctorDiagnosticReport>)));
        AssertProperties<SkillExportReport>(
            ("Host", typeof(string)),
            ("Tiers", typeof(IReadOnlyList<string>)),
            ("Format", typeof(string)),
            ("OutputPath", typeof(string)),
            ("Skills", typeof(IReadOnlyList<string>)),
            ("SkillCount", typeof(int)),
            ("ReloadGuidance", typeof(string)));
        AssertProperties<SkillHostArtifactReport>(
            ("Host", typeof(string)),
            ("Path", typeof(string)),
            ("Digest", typeof(string)),
            ("MaterializedFrontmatterDigest", typeof(string)));
        AssertProperties<SkillHostReport>(
            ("Host", typeof(string)),
            ("SupportsProjectScope", typeof(bool)),
            ("SupportsUserScope", typeof(bool)),
            ("ProjectDefaultTargetPath", typeof(string)),
            ("UserDefaultTargetPath", typeof(string)),
            ("UserTargetRootPolicy", typeof(SkillUserTargetRootPolicyReport)),
            ("RequiresMetadataArtifact", typeof(bool)),
            ("MetadataArtifactPath", typeof(string)),
            ("ReloadGuidance", typeof(string)));
        AssertProperties<SkillListReport>(
            ("Tiers", typeof(IReadOnlyList<string>)),
            ("AvailableTiers", typeof(IReadOnlyList<SkillListTierReport>)),
            ("Skills", typeof(IReadOnlyList<SkillListSkillReport>)),
            ("SupportedHosts", typeof(IReadOnlyList<SkillHostReport>)));
        AssertProperties<SkillListSkillReport>(
            ("SchemaVersion", typeof(int)),
            ("SkillName", typeof(string)),
            ("DisplayName", typeof(string)),
            ("Description", typeof(string)),
            ("Tier", typeof(string)),
            ("ContentDigest", typeof(string)),
            ("ManifestDigest", typeof(string)),
            ("HostArtifacts", typeof(IReadOnlyList<SkillHostArtifactReport>)));
        AssertProperties<SkillListTierReport>(
            ("Tier", typeof(string)),
            ("SkillCount", typeof(int)));
        AssertProperties<SkillOperationActionReport>(
            ("SkillName", typeof(string)),
            ("Action", typeof(string)),
            ("Status", typeof(string)),
            ("BlockedReason", typeof(string)),
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
            ("ChangeKind", typeof(string)),
            ("BeforeContent", typeof(string)),
            ("AfterContent", typeof(string)));
        AssertProperties<SkillOperationReport>(
            ("Host", typeof(string)),
            ("Tiers", typeof(IReadOnlyList<string>)),
            ("Scope", typeof(string)),
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
            ("Kind", typeof(string)),
            ("Code", typeof(string)),
            ("Message", typeof(string)),
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
    public void CreateInstallReport_RejectsUnsafeFileChangePath (string unsafePath)
    {
        var targetRoot = Path.GetFullPath("install-report-unsafe-file-change");
        var result = new SkillInstallResult(
            targetRoot,
            [
                new SkillInstallAction(CreateIdentity(targetRoot, "skill-a"), SkillInstallActionKind.Updated)
                {
                    FileChanges = new SkillActionFileChanges([unsafePath], []),
                },
            ]);

        Assert.Throws<ArgumentException>(() => SkillOperationReportBuilder.CreateInstallReport(result, CreateContext()));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("../escape.md")]
    [InlineData("/tmp/escape.md")]
    [InlineData("unsafe\\name.md")]
    [InlineData("C:/escape.md")]
    [InlineData("skill:C.md")]
    [InlineData("unsafe\u001fname.md")]
    public void CreateInstallReport_RejectsUnsafeFileDiffPath (string unsafePath)
    {
        var targetRoot = Path.GetFullPath("install-report-unsafe-file-diff");
        var result = new SkillInstallResult(
            targetRoot,
            [
                new SkillInstallAction(
                    CreateIdentity(targetRoot, "skill-a"),
                    SkillInstallActionKind.Updated,
                    Diffs:
                    [
                        new SkillActionDiff([new SkillFileDiff(unsafePath, SkillDiffChangeKind.Modified, "old", "new")]),
                    ]),
            ],
            PrintDiff: true);

        Assert.Throws<ArgumentException>(() => SkillOperationReportBuilder.CreateInstallReport(result, CreateContext()));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("../escape.md")]
    [InlineData("/tmp/escape.md")]
    [InlineData("unsafe\\name.md")]
    [InlineData("C:/escape.md")]
    [InlineData("skill:C.md")]
    [InlineData("unsafe\u001fname.md")]
    public void CreateInstallReport_RejectsUnsafeTargetFileSetPath (string unsafePath)
    {
        var targetRoot = Path.GetFullPath("install-report-unsafe-file-set");
        var result = new SkillInstallResult(
            targetRoot,
            [
                new SkillInstallAction(
                    CreateIdentity(targetRoot, "skill-a"),
                    SkillInstallActionKind.BlockedLocalModification,
                    SkillBlockedReason.LocalModificationRequiresForce,
                    TargetState: new SkillActionTargetState(
                        nameof(SkillInstalledTargetStateKind.FileSetDrift),
                        SkillFailureCodes.InstallTargetFileSetMismatch,
                        "File set drift.",
                        new SkillActionTargetFileSet([unsafePath], [], []))),
            ]);

        Assert.Throws<ArgumentException>(() => SkillOperationReportBuilder.CreateInstallReport(result, CreateContext()));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CreateInstallReport_RejectsMismatchedContext ()
    {
        var targetRoot = Path.GetFullPath("install-report-context-mismatch");
        var result = new SkillInstallResult(
            targetRoot,
            [new SkillInstallAction(CreateIdentity(targetRoot, "skill-a"), SkillInstallActionKind.NoOp)]);

        Assert.Throws<ArgumentException>(() => SkillOperationReportBuilder.CreateInstallReport(
            result,
            new SkillOperationReportContext(new ClaudeSkillHostAdapter().Descriptor, SkillScopeKind.Project, [new SkillTier("basic")])));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CreateInstallReport_RejectsMismatchedScope ()
    {
        var targetRoot = Path.GetFullPath("install-report-scope-mismatch");
        var result = new SkillInstallResult(
            targetRoot,
            [new SkillInstallAction(CreateIdentity(targetRoot, "skill-a", SkillScopeKind.Project), SkillInstallActionKind.NoOp)]);

        Assert.Throws<ArgumentException>(() => SkillOperationReportBuilder.CreateInstallReport(
            result,
            CreateContext(SkillScopeKind.User)));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CreateInstallReport_RejectsMismatchedTargetRoot ()
    {
        var targetRoot = Path.GetFullPath("install-report-target-root-mismatch");
        var result = new SkillInstallResult(
            Path.GetFullPath("install-report-other-target-root"),
            [new SkillInstallAction(CreateIdentity(targetRoot, "skill-a"), SkillInstallActionKind.NoOp)]);

        Assert.Throws<ArgumentException>(() => SkillOperationReportBuilder.CreateInstallReport(
            result,
            CreateContext()));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CreateInstallReport_RejectsUnsupportedTargetStateKind ()
    {
        var targetRoot = Path.GetFullPath("install-report-invalid-target-state");
        var result = new SkillInstallResult(
            targetRoot,
            [
                new SkillInstallAction(
                    CreateIdentity(targetRoot, "skill-a"),
                    SkillInstallActionKind.BlockedLocalModification,
                    SkillBlockedReason.LocalModificationRequiresForce,
                    TargetState: new SkillActionTargetState("unsupported-state")),
            ]);

        Assert.Throws<ArgumentException>(() => SkillOperationReportBuilder.CreateInstallReport(
            result,
            CreateContext()));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CreateInstallReport_RejectsMissingReloadGuidance ()
    {
        var targetRoot = Path.GetFullPath("install-report-missing-reload-guidance");
        var result = new SkillInstallResult(
            targetRoot,
            [new SkillInstallAction(CreateIdentity(targetRoot, "skill-a"), SkillInstallActionKind.NoOp)]);
        var descriptor = OpenAiDescriptor with { ReloadGuidance = string.Empty };

        Assert.Throws<ArgumentException>(() => SkillOperationReportBuilder.CreateInstallReport(
            result,
            new SkillOperationReportContext(descriptor, SkillScopeKind.Project, [new SkillTier("basic")])));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task CreateExportReport_RejectsMissingReloadGuidance ()
    {
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var descriptor = OpenAiDescriptor with { ReloadGuidance = string.Empty };

        Assert.Throws<ArgumentException>(() => SkillOperationReportBuilder.CreateExportReport(
            "/tmp/agent-skills.zip",
            packages,
            descriptor,
            SkillExportFormat.Zip,
            [new SkillTier("basic")]));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task CreateExportReport_RejectsMissingHostKey ()
    {
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var descriptor = OpenAiDescriptor with { HostKey = string.Empty };

        Assert.Throws<ArgumentException>(() => SkillOperationReportBuilder.CreateExportReport(
            "/tmp/agent-skills.zip",
            packages,
            descriptor,
            SkillExportFormat.Zip,
            [new SkillTier("basic")]));
    }

    private static SkillInstallIdentity CreateIdentity (
        string targetRoot,
        string skillName,
        SkillScopeKind scope = SkillScopeKind.Project)
    {
        return new SkillInstallIdentity(
            OpenAiSkillHostAdapter.HostKey,
            scope,
            targetRoot,
            skillName);
    }

    private static SkillHostDescriptor OpenAiDescriptor => new OpenAiSkillHostAdapter().Descriptor;

    private static SkillOperationReportContext CreateContext ()
    {
        return CreateContext([new SkillTier("basic")]);
    }

    private static SkillOperationReportContext CreateContext (SkillScopeKind scope)
    {
        return CreateContext(scope, [new SkillTier("basic")]);
    }

    private static SkillOperationReportContext CreateContext (IReadOnlyList<SkillTier> tiers)
    {
        return CreateContext(SkillScopeKind.Project, tiers);
    }

    private static SkillOperationReportContext CreateContext (
        SkillScopeKind scope,
        IReadOnlyList<SkillTier> tiers)
    {
        return new SkillOperationReportContext(OpenAiDescriptor, scope, tiers);
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

        if (type.IsPrimitive || type == typeof(string) || type.Namespace == "MackySoft.AgentSkills.OperationReports.Contracts")
        {
            return [];
        }

        return [type];
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
