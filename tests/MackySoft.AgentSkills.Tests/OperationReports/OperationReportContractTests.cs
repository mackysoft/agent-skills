using System.Reflection;
using System.Text.Json;
using MackySoft.AgentSkills.Digests;
using MackySoft.AgentSkills.Distribution;
using MackySoft.AgentSkills.Doctor;
using MackySoft.AgentSkills.Hosts.Contracts;
using MackySoft.AgentSkills.Installation.Results;
using MackySoft.AgentSkills.Installation.Targeting;
using MackySoft.AgentSkills.OperationReports.Contracts;
using MackySoft.AgentSkills.OperationReports.Literals;
using MackySoft.AgentSkills.Shared;
using MackySoft.AgentSkills.Shared.Text;

namespace MackySoft.AgentSkills.Tests.OperationReports;

public sealed class OperationReportContractTests
{
    private static readonly Sha256Digest Digest = Sha256Digest.Parse(new string('a', 64));
    private static readonly string RepositoryRoot = Path.GetFullPath("operation-report-repository");
    private static readonly string TargetRoot = Path.Combine(RepositoryRoot, ".agents", "skills");

    [Fact]
    [Trait("Size", "Small")]
    public void ReportContracts_ExposeOnlyGetPropertiesAndNoPublicConstruction ()
    {
        var reportTypes = typeof(SkillOperationReport).Assembly.GetTypes()
            .Where(static type =>
                type.IsPublic
                && string.Equals(type.Namespace, "MackySoft.AgentSkills.OperationReports.Contracts", StringComparison.Ordinal)
                && type.Name.EndsWith("Report", StringComparison.Ordinal))
            .ToArray();

        Assert.NotEmpty(reportTypes);
        Assert.All(reportTypes, static type =>
        {
            Assert.True(type.IsSealed);
            Assert.Empty(type.GetConstructors(BindingFlags.Instance | BindingFlags.Public));
            Assert.All(type.GetProperties(BindingFlags.Instance | BindingFlags.Public), static property => Assert.Null(property.SetMethod));
        });
    }

    [Fact]
    [Trait("Size", "Small")]
    public void FileCollectionReports_SnapshotValidateAndSortPaths ()
    {
        var replacedFiles = new List<string> { "z.txt", "a.txt" };
        var removedFiles = new List<string> { "local.md" };
        var report = new SkillOperationFileChangesReport(replacedFiles, removedFiles);

        replacedFiles.Clear();
        removedFiles[0] = "changed.md";

        Assert.Equal(["a.txt", "z.txt"], report.ReplacedFiles);
        Assert.Equal(["local.md"], report.RemovedFiles);
        Assert.Throws<ArgumentException>(() => new SkillOperationFileChangesReport(["../escape.md"], []));
        Assert.Throws<ArgumentException>(() => new SkillTargetFileSetReport([], ["unsafe\\name.md"], []));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ActionReport_SnapshotsFileDiffsAndRejectsNullItems ()
    {
        var diffs = new List<SkillOperationFileDiffReport>
        {
            new("SKILL.md", SkillDiffChangeKind.Modified, "before", "after"),
        };
        var report = new SkillOperationActionReport("skill-a", "updated", SkillOperationActionStatus.Changed, null, null, null, diffs);

        diffs.Clear();

        Assert.Single(report.FileDiffs);
        Assert.Throws<ArgumentException>(() => new SkillOperationActionReport(
            "skill-a",
            "updated",
            SkillOperationActionStatus.Changed,
            null,
            null,
            null,
            [null!]));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void DoctorReport_ComputesHealthFromDiagnosticSnapshot ()
    {
        var diagnostics = new List<SkillDoctorDiagnosticReport>
        {
            new(SkillDoctorSeverity.Info, "SKILL_DOCTOR_OK", "Healthy.", null, null),
        };
        var healthy = new SkillDoctorReport(
            SkillHostKind.OpenAi,
            [],
            [],
            SkillScopeKind.Project,
            RepositoryRoot,
            TargetRoot,
            "Reload.",
            diagnostics);

        diagnostics[0] = new SkillDoctorDiagnosticReport(
            SkillDoctorSeverity.Error,
            "SKILL_DOCTOR_ERROR",
            "Broken.",
            null,
            null);
        var unhealthy = new SkillDoctorReport(
            SkillHostKind.OpenAi,
            [],
            [],
            SkillScopeKind.Project,
            RepositoryRoot,
            TargetRoot,
            "Reload.",
            diagnostics);

        Assert.True(healthy.IsHealthy);
        Assert.False(unhealthy.IsHealthy);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TargetReports_RejectPathsThatDoNotMatchTheirScopeContract ()
    {
        Assert.Throws<ArgumentNullException>(() => new SkillDoctorReport(
            SkillHostKind.OpenAi,
            [],
            [],
            SkillScopeKind.Project,
            repositoryRoot: null,
            TargetRoot,
            "Reload.",
            []));
        Assert.Throws<ArgumentException>(() => new SkillDoctorReport(
            SkillHostKind.OpenAi,
            [],
            [],
            SkillScopeKind.User,
            RepositoryRoot,
            TargetRoot,
            "Reload.",
            []));
        Assert.Throws<ArgumentException>(() => new SkillDoctorReport(
            SkillHostKind.OpenAi,
            [],
            [],
            SkillScopeKind.Project,
            "relative-repository",
            TargetRoot,
            "Reload.",
            []));
        Assert.Throws<ArgumentException>(() => new SkillOperationReport(
            SkillHostKind.OpenAi,
            [],
            [],
            SkillScopeKind.Project,
            RepositoryRoot,
            "relative-target",
            dryRun: false,
            force: false,
            reloadGuidance: "Reload.",
            actions: [],
            actionCounts: [],
            statusCounts: []));
        Assert.Throws<ArgumentException>(() => new SkillDoctorReport(
            SkillHostKind.OpenAi,
            [],
            [],
            SkillScopeKind.Project,
            RepositoryRoot,
            Path.GetFullPath(RepositoryRoot + "-outside"),
            "Reload.",
            []));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TargetReports_NormalizeRepositoryAndTargetPaths ()
    {
        var repositoryRootInput = Path.Combine(RepositoryRoot, "nested", "..");
        var targetRootInput = Path.Combine(repositoryRootInput, ".agents", "nested", "..", "skills");
        var doctor = new SkillDoctorReport(
            SkillHostKind.OpenAi,
            [],
            [],
            SkillScopeKind.Project,
            repositoryRootInput,
            targetRootInput,
            "Reload.",
            []);
        var operation = new SkillOperationReport(
            SkillHostKind.OpenAi,
            [],
            [],
            SkillScopeKind.Project,
            repositoryRootInput,
            targetRootInput,
            dryRun: false,
            force: false,
            reloadGuidance: "Reload.",
            actions: [],
            actionCounts: [],
            statusCounts: []);

        Assert.Equal(RepositoryRoot, doctor.RepositoryRoot);
        Assert.Equal(TargetRoot, doctor.TargetRoot);
        Assert.Equal(RepositoryRoot, operation.RepositoryRoot);
        Assert.Equal(TargetRoot, operation.TargetRoot);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ReportContractLiteralEnums_SerializeAsCanonicalStrings ()
    {
        var operation = new SkillOperationReport(
            SkillHostKind.OpenAi,
            [],
            [],
            SkillScopeKind.Project,
            RepositoryRoot,
            TargetRoot,
            dryRun: false,
            force: false,
            reloadGuidance: "Reload.",
            actions:
            [
                new SkillOperationActionReport(
                    "skill-a",
                    "updated",
                    SkillOperationActionStatus.Blocked,
                    SkillBlockedReason.LocalModificationRequiresForce,
                    new SkillTargetStateReport(new SkillActionTargetState(
                        SkillTargetStateKind.CommonContentDrift,
                        SkillFailureCodes.InstallTargetContentDigestMismatch,
                        "Content drift.",
                        fileSet: null,
                        installedSkillBundleVersion: null,
                        bundledSkillBundleVersion: 1)),
                    fileChanges: null,
                    fileDiffs: [new SkillOperationFileDiffReport("SKILL.md", SkillDiffChangeKind.Modified, "before", "after")]),
            ],
            actionCounts: [],
            statusCounts: []);
        var doctor = new SkillDoctorReport(
            SkillHostKind.OpenAi,
            [],
            [],
            SkillScopeKind.Project,
            RepositoryRoot,
            TargetRoot,
            "Reload.",
            [
                new SkillDoctorDiagnosticReport(
                    SkillDoctorSeverity.Error,
                    "SKILL_CONTENT_DRIFT",
                    "Content drift.",
                    "skill-a",
                    SkillTargetStateKind.CommonContentDrift),
            ]);
        var export = new SkillExportReport(
            SkillHostKind.OpenAi,
            [],
            [],
            SkillExportFormat.Zip,
            Path.Combine(TargetRoot, "skills.zip"),
            [],
            0,
            "Reload.");
        var options = new JsonSerializerOptions();
        options.Converters.Add(new ContractLiteralJsonConverterFactory());

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(new { Operation = operation, Doctor = doctor, Export = export }, options));

        var operationJson = document.RootElement.GetProperty("Operation");
        Assert.Equal("openai", operationJson.GetProperty("Host").GetString());
        Assert.Equal("project", operationJson.GetProperty("Scope").GetString());
        var actionJson = operationJson.GetProperty("Actions")[0];
        Assert.Equal("blocked", actionJson.GetProperty("Status").GetString());
        Assert.Equal("localModificationRequiresForce", actionJson.GetProperty("BlockedReason").GetString());
        Assert.Equal("commonContentDrift", actionJson.GetProperty("TargetState").GetProperty("Kind").GetString());
        Assert.Equal("modified", actionJson.GetProperty("FileDiffs")[0].GetProperty("ChangeKind").GetString());

        var doctorJson = document.RootElement.GetProperty("Doctor");
        Assert.Equal("error", doctorJson.GetProperty("Diagnostics")[0].GetProperty("Severity").GetString());
        Assert.Equal("commonContentDrift", doctorJson.GetProperty("Diagnostics")[0].GetProperty("TargetState").GetString());
        Assert.Equal("zip", document.RootElement.GetProperty("Export").GetProperty("Format").GetString());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ReportContracts_RejectUndefinedLiteralEnums ()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SkillDoctorDiagnosticReport(
            (SkillDoctorSeverity)42,
            "SKILL_ERROR",
            "Broken.",
            null,
            null));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SkillDoctorReport(
            (SkillHostKind)42,
            [],
            [],
            SkillScopeKind.Project,
            RepositoryRoot,
            TargetRoot,
            "Reload.",
            []));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SkillDoctorReport(
            SkillHostKind.OpenAi,
            [],
            [],
            (SkillScopeKind)42,
            RepositoryRoot,
            TargetRoot,
            "Reload.",
            []));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SkillExportReport(
            SkillHostKind.OpenAi,
            [],
            [],
            (SkillExportFormat)42,
            "/target",
            [],
            0,
            "Reload."));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SkillOperationActionReport(
            "skill-a",
            "updated",
            (SkillOperationActionStatus)42,
            null,
            null,
            null,
            []));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SkillOperationActionReport(
            "skill-a",
            "updated",
            SkillOperationActionStatus.Blocked,
            (SkillBlockedReason)42,
            null,
            null,
            []));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SkillOperationFileDiffReport(
            "SKILL.md",
            (SkillDiffChangeKind)42,
            null,
            "after"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SkillHostArtifactReport((SkillHostKind)42, null, null, Digest));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(SkillDiffChangeKind.Added, "before", "after")]
    [InlineData(SkillDiffChangeKind.Added, null, null)]
    [InlineData(SkillDiffChangeKind.Modified, null, "after")]
    [InlineData(SkillDiffChangeKind.Deleted, "before", "after")]
    public void FileDiffReport_RejectsContentsThatDoNotMatchChangeKind (
        SkillDiffChangeKind changeKind,
        string? beforeContent,
        string? afterContent)
    {
        Assert.Throws<ArgumentException>(() => new SkillOperationFileDiffReport(
            "SKILL.md",
            changeKind,
            beforeContent,
            afterContent));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TargetStateReport_ProjectsValidatedActionState ()
    {
        var state = new SkillActionTargetState(
            SkillTargetStateKind.FileSetDrift,
            SkillFailureCodes.InstallTargetFileSetMismatch,
            "File-set drift.",
            new SkillActionTargetFileSet(["missing.md"], [], []),
            installedSkillBundleVersion: null,
            bundledSkillBundleVersion: 1);

        var report = new SkillTargetStateReport(state);

        Assert.Equal(state.Kind, report.Kind);
        Assert.Equal(state.Code!.Value, report.Code);
        Assert.Equal(state.Message, report.Message);
        Assert.Null(report.InstalledSkillBundleVersion);
        Assert.Equal(1, report.BundledSkillBundleVersion);
        Assert.Equal(["missing.md"], report.FileSet!.MissingFiles);
        Assert.Throws<ArgumentNullException>(() => new SkillTargetStateReport(null!));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void HostArtifactReport_RequiresConsistentArtifactValues ()
    {
        Assert.Throws<ArgumentException>(() => new SkillHostArtifactReport(SkillHostKind.OpenAi, "agents/openai.yaml", null, Digest));
        Assert.Throws<ArgumentException>(() => new SkillHostArtifactReport(SkillHostKind.OpenAi, null, Digest, Digest));
        Assert.Throws<ArgumentException>(() => new SkillHostArtifactReport(SkillHostKind.OpenAi, "../escape.yaml", Digest, Digest));
        Assert.Throws<ArgumentNullException>(() => new SkillHostArtifactReport(SkillHostKind.OpenAi, null, null, null!));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void UserTargetRootPolicyReport_RequiresConsistentSafeDirectories ()
    {
        Assert.Throws<ArgumentException>(() => new SkillUserTargetRootPolicyReport(null, "skills", ".codex/skills"));
        Assert.Throws<ArgumentException>(() => new SkillUserTargetRootPolicyReport("CODEX_HOME", "../skills", ".codex/skills"));
        Assert.Throws<ArgumentException>(() => new SkillUserTargetRootPolicyReport(null, null, "../skills"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CountAndListSkillReports_RejectInvalidNumericAndMissingDigestValues ()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SkillOperationCountReport("changed", -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateListSkillReport(schemaVersion: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateListSkillReport(skillBundleVersion: 0));
        Assert.Throws<ArgumentNullException>(() => CreateListSkillReport(null!, Digest));
        Assert.Throws<ArgumentNullException>(() => CreateListSkillReport(Digest, null!));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ReportDigestValues_SerializeAsCanonicalJsonStrings ()
    {
        var contentDigest = Sha256Digest.Parse(new string('b', 64));
        var manifestDigest = Sha256Digest.Parse(new string('c', 64));
        var artifactDigest = Sha256Digest.Parse(new string('d', 64));
        var frontmatterDigest = Sha256Digest.Parse(new string('e', 64));
        var report = CreateListSkillReport(
            contentDigest,
            manifestDigest,
            [new SkillHostArtifactReport(SkillHostKind.OpenAi, "agents/openai.yaml", artifactDigest, frontmatterDigest)]);

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(report));

        Assert.Equal(contentDigest.ToString(), document.RootElement.GetProperty("ContentDigest").GetString());
        Assert.Equal(manifestDigest.ToString(), document.RootElement.GetProperty("ManifestDigest").GetString());
        var artifact = document.RootElement.GetProperty("HostArtifacts")[0];
        Assert.Equal(artifactDigest.ToString(), artifact.GetProperty("Digest").GetString());
        Assert.Equal(frontmatterDigest.ToString(), artifact.GetProperty("MaterializedFrontmatterDigest").GetString());
    }

    private static SkillListSkillReport CreateListSkillReport (
        int schemaVersion = 1,
        int skillBundleVersion = 1)
    {
        return CreateListSkillReport(Digest, Digest, [], schemaVersion, skillBundleVersion);
    }

    private static SkillListSkillReport CreateListSkillReport (
        Sha256Digest contentDigest,
        Sha256Digest manifestDigest,
        IReadOnlyList<SkillHostArtifactReport>? hostArtifacts = null,
        int schemaVersion = 1,
        int skillBundleVersion = 1)
    {
        return new SkillListSkillReport(
            schemaVersion,
            skillBundleVersion,
            "skill-a",
            "Skill A",
            "Description",
            [],
            "basic",
            "catalog-a",
            contentDigest,
            manifestDigest,
            hostArtifacts ?? []);
    }
}
