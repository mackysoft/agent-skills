using System.Text.Json;
using MackySoft.AgentSkills.Distribution;
using MackySoft.AgentSkills.Doctor;
using MackySoft.AgentSkills.Hosts.OpenAi;
using MackySoft.AgentSkills.Installation.Results;
using MackySoft.AgentSkills.Installation.State;
using MackySoft.AgentSkills.Installation.Targeting;
using MackySoft.AgentSkills.OperationReports;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Tests.OperationReports;

public sealed class SkillOperationReportBuilderTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void CreateInstallReport_ProjectsActionsCountsAndFileDetails ()
    {
        var targetRoot = Path.GetFullPath("install-report-target");
        var context = new SkillOperationReportContext(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project);
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
        Assert.Equal("project", report.Scope);
        Assert.Equal(targetRoot, report.TargetRoot);
        Assert.True(report.DryRun);
        Assert.True(report.Force);
        Assert.True(report.PrintDiff);
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

        AssertCount(report.ActionCounts, "created", 0);
        AssertCount(report.ActionCounts, "updated", 1);
        AssertCount(report.ActionCounts, "noOp", 1);
        AssertCount(report.ActionCounts, "blockedLocalModification", 1);
        AssertCount(report.StatusCounts, "changed", 1);
        AssertCount(report.StatusCounts, "noOp", 1);
        AssertCount(report.StatusCounts, "skipped", 0);
        AssertCount(report.StatusCounts, "blocked", 1);

        var firstJson = JsonSerializer.Serialize(report);
        var secondJson = JsonSerializer.Serialize(report);
        Assert.Equal(firstJson, secondJson);
        Assert.DoesNotContain("repositoryRoot", firstJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("command", firstJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("exitCode", firstJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CreateUpdateReport_ProjectsBlockedUnmanagedCounts ()
    {
        var targetRoot = Path.GetFullPath("update-report-target");
        var context = new SkillOperationReportContext(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.User);
        var result = new SkillUpdateResult(
            targetRoot,
            [
                new SkillUpdateAction(CreateIdentity(targetRoot, "skill-b", SkillScopeKind.User), SkillUpdateActionKind.BlockedUnmanaged, SkillBlockedReason.UnmanagedTarget),
                new SkillUpdateAction(CreateIdentity(targetRoot, "skill-a", SkillScopeKind.User), SkillUpdateActionKind.Created)
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
        Assert.Equal("blockedUnmanaged", report.Actions[1].Action);
        Assert.Equal("blocked", report.Actions[1].Status);
        Assert.Equal("unmanagedTarget", report.Actions[1].BlockedReason);
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
        var context = new SkillOperationReportContext(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project);
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

        Assert.False(report.PrintDiff);
        Assert.Equal("deleted", report.Actions[0].Action);
        Assert.Equal("changed", report.Actions[0].Status);
        Assert.Equal(["SKILL.md", "agent-skill.json"], report.Actions[0].FileChanges!.RemovedFiles);
        Assert.Equal("skippedUnmanaged", report.Actions[1].Action);
        Assert.Equal("skipped", report.Actions[1].Status);
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

        var report = SkillOperationReportBuilder.CreateListReport(packages, hostAdapters);

        Assert.Equal(SkillTestData.ExpectedSkillNames, report.Skills.Select(static skill => skill.SkillName).ToArray());
        Assert.Equal(["claude", "copilot", "openai"], report.SupportedHosts.Select(static host => host.HostKey).ToArray());
        var openAi = report.SupportedHosts.Single(static host => host.HostKey == OpenAiSkillHostAdapter.HostKey);
        Assert.True(openAi.SupportsProjectScope);
        Assert.True(openAi.SupportsUserScope);
        Assert.True(openAi.RequiresMetadataArtifact);
        Assert.Equal("agents/openai.yaml", openAi.MetadataArtifactPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task CreateExportReport_ProjectsFormatAndSortedSkillNames ()
    {
        var packages = (await SkillTestData.GenerateFixturePackagesAsync()).Reverse().ToArray();

        var report = SkillOperationReportBuilder.CreateExportReport(
            "/tmp/agent-skills.zip",
            packages,
            OpenAiSkillHostAdapter.HostKey,
            SkillExportFormat.Zip);

        Assert.Equal(OpenAiSkillHostAdapter.HostKey, report.Host);
        Assert.Equal("zip", report.Format);
        Assert.Equal("/tmp/agent-skills.zip", report.OutputPath);
        Assert.Equal(SkillTestData.ExpectedSkillNames, report.Skills);
        Assert.Equal(SkillTestData.ExpectedSkillNames.Length, report.SkillCount);
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
                SkillDoctorDiagnostic.Info(
                    "SKILL_DOCTOR_OK",
                    "Healthy.",
                    "skill-a"),
            ]);

        var report = SkillOperationReportBuilder.CreateDoctorReport(result, SkillScopeKind.Project);

        Assert.False(report.IsHealthy);
        Assert.Equal("project", report.Scope);
        Assert.Equal(new string?[] { null, "skill-a", "skill-b" }, report.Diagnostics.Select(static diagnostic => diagnostic.SkillName).ToArray());
        Assert.Equal("error", report.Diagnostics[0].Severity);
        Assert.Equal("unmanagedTarget", report.Diagnostics[0].TargetState);
        Assert.Equal("info", report.Diagnostics[1].Severity);
        Assert.Null(report.Diagnostics[1].TargetState);
        Assert.Equal("hostArtifactDrift", report.Diagnostics[2].TargetState);
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
            new SkillOperationReportContext("claude", SkillScopeKind.Project)));
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

    private static void AssertCount (
        IReadOnlyList<SkillOperationCountReport> counts,
        string literal,
        int expected)
    {
        Assert.Equal(expected, counts.Single(count => string.Equals(count.Literal, literal, StringComparison.Ordinal)).Count);
    }
}
