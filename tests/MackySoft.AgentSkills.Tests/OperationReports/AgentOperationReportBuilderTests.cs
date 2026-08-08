using MackySoft.AgentSkills.Agents;
using MackySoft.AgentSkills.Distribution;
using MackySoft.AgentSkills.OperationReports.Projection;
using MackySoft.AgentSkills.Tests.Distribution;

namespace MackySoft.AgentSkills.Tests.OperationReports;

public sealed class AgentOperationReportBuilderTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task CreateListReport_ProjectsSelectedAgentsHostArtifactsAndResolvedSkillsDeterministically ()
    {
        var skills = await SkillTestData.GenerateFixturePackagesAsync();
        var planner = AgentDistributionTestData.CreateAgent(skills, "planning", "planner", AgentHostKind.OpenAi, "planner.toml");
        var reviewer = AgentDistributionTestData.CreateAgent(skills, "quality", "reviewer", AgentHostKind.OpenAi, "reviewer.toml");
        var catalog = AgentDistributionTestData.CreateCatalog(
            skills.Reverse().ToArray(),
            [reviewer, planner],
            [new AgentCategory("quality"), new AgentCategory("planning")],
            [reviewer.Manifest.AgentName]);

        var report = AgentOperationReportBuilder.CreateListReport(catalog);

        Assert.Equal(["quality", "planning"], report.Categories);
        Assert.Equal(["reviewer"], report.AgentNames);
        Assert.Equal(["planner", "reviewer"], report.Agents.Select(static agent => agent.AgentName));
        Assert.Equal(SkillTestData.ExpectedSkillNames, report.ResolvedSkills);
        Assert.Equal([AgentHostKind.OpenAi], report.SupportedHostIds);
        Assert.All(report.Agents, static agent =>
        {
            Assert.Equal(1, agent.BundleVersion);
            Assert.Equal("com.mackysoft.agent-skills", agent.CatalogId);
            Assert.NotEmpty(agent.SkillDependencies);
            Assert.Single(agent.HostArtifacts);
        });
        var plannerReport = report.Agents[0];
        Assert.Equal(planner.Manifest.SchemaVersion, plannerReport.SchemaVersion);
        Assert.Equal(planner.Manifest.DisplayName, plannerReport.DisplayName);
        Assert.Equal(planner.Manifest.Description, plannerReport.Description);
        Assert.Equal(planner.Manifest.Category.Value, plannerReport.Category);
        Assert.Equal(planner.Manifest.SkillDependencies.Select(static skill => skill.Value), plannerReport.SkillDependencies);
        Assert.Equal(planner.Manifest.ContentDigest, plannerReport.ContentDigest);
        Assert.Equal(planner.Manifest.ManifestDigest, plannerReport.ManifestDigest);
        Assert.Equal(AgentHostKind.OpenAi, plannerReport.HostArtifacts[0].HostId);
        Assert.Equal("hosts/openai/planner.toml", plannerReport.HostArtifacts[0].Path);
        Assert.Equal(planner.Manifest.HostArtifacts[0].Digest, plannerReport.HostArtifacts[0].Digest);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task CreateExportReport_ProjectsSuccessfulExportSelectionAndCounts ()
    {
        var skills = await SkillTestData.GenerateFixturePackagesAsync();
        var agent = AgentDistributionTestData.CreateAgent(skills, "planning", "planner", AgentHostKind.OpenAi, "planner.toml");
        var catalog = AgentDistributionTestData.CreateCatalog(
            skills,
            [agent],
            [agent.Manifest.Category],
            [agent.Manifest.AgentName]);
        var outputPath = Path.Combine("tmp", "agents.zip");

        var report = AgentOperationReportBuilder.CreateExportReport(
            outputPath,
            catalog,
            AgentHostKind.OpenAi,
            SkillExportFormat.Zip);

        Assert.Equal(AgentHostKind.OpenAi, report.HostId);
        Assert.Equal(["planning"], report.Categories);
        Assert.Equal(["planner"], report.AgentNames);
        Assert.Equal(SkillExportFormat.Zip, report.Format);
        Assert.Equal(Path.GetFullPath(outputPath), report.OutputPath);
        Assert.Equal(["planner"], report.Agents);
        Assert.Equal(1, report.AgentCount);
        Assert.Equal(SkillTestData.ExpectedSkillNames, report.Skills);
        Assert.Equal(SkillTestData.ExpectedSkillNames.Length, report.SkillCount);
    }
}
