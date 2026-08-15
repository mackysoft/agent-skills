using MackySoft.AgentDistribution.Agents.Installation.Requests;
using MackySoft.AgentDistribution.Agents.Installation.Targeting;
using MackySoft.AgentDistribution.Distribution;
using MackySoft.AgentDistribution.Installation.Targeting;
using MackySoft.AgentDistribution.OperationReports.Contracts;
using MackySoft.AgentDistribution.OperationReports.Literals;
using MackySoft.AgentDistribution.OperationReports.Projection;
using MackySoft.AgentDistribution.Tests.Agents;
using MackySoft.AgentDistribution.Tests.Distribution;

namespace MackySoft.AgentDistribution.Tests.OperationReports;

public sealed class AgentOperationReportBuilderTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task CreateListReport_ProjectsSelectedAgentsHostArtifactsAndResolvedSkillsDeterministically ()
    {
        var skills = await SkillTestData.GenerateFixturePackagesAsync();
        var planner = AgentDistributionTestData.CreateAgent(skills, "planner", HostKind.Codex, "planner.toml");
        var reviewer = AgentDistributionTestData.CreateAgent(skills, "reviewer", HostKind.Codex, "reviewer.toml");
        var catalog = AgentDistributionTestData.CreateCatalog(
            skills.Reverse().ToArray(),
            [reviewer, planner],
            [reviewer.Manifest.AgentName]);

        var report = AgentOperationReportBuilder.CreateListReport(catalog);

        Assert.Equal(["reviewer"], report.AgentNames);
        Assert.Equal(["planner", "reviewer"], report.Agents.Select(static agent => agent.AgentName));
        Assert.Equal(SkillTestData.ExpectedSkillNames, report.ResolvedSkills);
        Assert.Equal([HostKind.Codex], report.SupportedHostIds);
        Assert.All(report.Agents, static agent =>
        {
            Assert.Equal(1, agent.BundleVersion);
            Assert.Equal("com.mackysoft.agent-distribution", agent.CatalogId);
            Assert.NotEmpty(agent.SkillDependencies);
            Assert.Single(agent.HostArtifacts);
        });
        var plannerReport = report.Agents[0];
        Assert.Equal(planner.Manifest.SchemaVersion, plannerReport.SchemaVersion);
        Assert.Equal(planner.Manifest.DisplayName, plannerReport.DisplayName);
        Assert.Equal(planner.Manifest.Description, plannerReport.Description);
        Assert.Equal(planner.Manifest.SkillDependencies.Select(static skill => skill.Value), plannerReport.SkillDependencies);
        Assert.Equal(planner.Manifest.ContentDigest, plannerReport.ContentDigest);
        Assert.Equal(planner.Manifest.ManifestDigest, plannerReport.ManifestDigest);
        Assert.Equal(HostKind.Codex, plannerReport.HostArtifacts[0].HostId);
        Assert.Equal("hosts/codex/planner.toml", plannerReport.HostArtifacts[0].Path);
        Assert.Equal(planner.Manifest.HostArtifacts[0].Digest, plannerReport.HostArtifacts[0].Digest);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task CreateExportReport_ProjectsSuccessfulExportSelectionAndCounts ()
    {
        var skills = await SkillTestData.GenerateFixturePackagesAsync();
        var agent = AgentDistributionTestData.CreateAgent(skills, "planner", HostKind.Codex, "planner.toml");
        var catalog = AgentDistributionTestData.CreateCatalog(
            skills,
            [agent],
            [agent.Manifest.AgentName]);
        var outputPath = Path.Combine("tmp", "agents.zip");

        var report = AgentOperationReportBuilder.CreateExportReport(
            AbsolutePath.Parse(Path.GetFullPath(outputPath)),
            catalog,
            HostKind.Codex,
            PackageExportFormat.Zip);

        Assert.Equal(HostKind.Codex, report.HostId);
        Assert.Equal(["planner"], report.AgentNames);
        Assert.Equal(PackageExportFormat.Zip, report.Format);
        Assert.Equal(Path.GetFullPath(outputPath), report.OutputPath);
        Assert.Equal(["planner"], report.Agents);
        Assert.Equal(1, report.AgentCount);
        Assert.Equal(SkillTestData.ExpectedSkillNames, report.Skills);
        Assert.Equal(SkillTestData.ExpectedSkillNames.Length, report.SkillCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task CreateInstallReport_ProjectsAgentAndResolvedSkillPlansThroughPublicReportContracts ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-reports", "agent-install");
        var skills = await SkillTestData.GenerateFixturePackagesAsync();
        var agent = AgentOperationTestData.CreateAgent(
            skills,
            "architect",
            "architect.toml",
            "name = \"architect\"\n",
            [skills[0].Manifest.SkillName]);
        var catalog = AgentOperationTestData.CreateCatalog(skills, [agent], [skills[0]], [agent.Manifest.AgentName]);
        var agentTarget = SkillTestData.CreateAgentTargetRequest(HostKind.Codex, AgentInstallScopeKind.Project, scope.FullPath, "agents");
        var skillTarget = SkillTestData.CreateInstallRequest(HostKind.Codex, SkillScopeKind.Project, scope.FullPath, "skills");
        var result = await AgentOperationTestData.CreateInstallService(scope.FullPath).InstallAsync(
            new AgentInstallInput(catalog, agentTarget, skillTarget, dryRun: true, printDiff: true));
        Assert.True(result.IsSuccess, result.Failure?.Message);
        var context = new AgentOperationReportContext(
            HostKind.Codex,
            AgentInstallScopeKind.Project,
            scope.FullPath,
            catalog.SelectedAgentNames,
            new SkillOperationReportContext(
                ResolveHost(HostKind.Codex),
                SkillScopeKind.Project,
                scope.FullPath,
                [],
                [skills[0].Manifest.SkillName]));

        AgentOperationReport report = AgentOperationReportBuilder.CreateInstallReport(result.Value!, context);

        Assert.True(report.DryRun);
        Assert.Equal(HostKind.Codex, report.Host);
        Assert.Equal(OperationScopeKind.Project, report.Scope);
        Assert.Equal(["architect"], report.AgentNames);
        Assert.Equal(OperationActionStatus.Changed, Assert.Single(report.Actions).Status);
        Assert.Equal(1, report.ActionCounts.Single(static count => count.Literal == "created").Count);
        Assert.Equal(1, report.StatusCounts.Single(static count => count.Literal == "changed").Count);
        Assert.NotNull(report.SkillReport);
        Assert.True(report.SkillReport.DryRun);
        Assert.Equal([skills[0].Manifest.SkillName.Value], report.SkillReport.SkillNames);
    }
    private static SkillResolvedHost ResolveHost (HostKind host)
    {
        var result = SkillTestData.CreateInstallTargetResolver().ResolveHost(host);
        Assert.True(result.IsSuccess, result.Failure?.Message);
        return result.Value!;
    }
}
