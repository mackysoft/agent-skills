using MackySoft.AgentDistribution.Bundles;
using MackySoft.AgentDistribution.Sources;
using MackySoft.Tests;

namespace MackySoft.AgentDistribution.Tests;

public sealed class ReadmeContractTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task SourceBundleExample_IsAcceptedAsCanonicalInput ()
    {
        var readme = ReadReadme();
        var bundleJson = ReadJsonCodeBlock(readme, "Create `bundle.json`");
        using var scope = TestDirectories.CreateTempScope("agent-distribution-readme", "bundle-json");
        scope.WriteFile("bundle.json", bundleJson);
        var reader = new AgentDistributionBundleDefinitionReader(new AgentDistributionBundleJsonSerializer());

        var result = await reader.ReadAsync(AbsolutePath.Parse(scope.FullPath), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SourceSkillExample_IsAcceptedAsSourceMetadata ()
    {
        var readme = ReadReadme();
        var skillJson = ReadJsonCodeBlock(readme, "For each skill, create");
        using var scope = TestDirectories.CreateTempScope("agent-distribution-readme", "skill-json");
        var skillDirectory = scope.CreateDirectory("basic/example-review");
        scope.WriteFile("basic/example-review/skill.json", skillJson);
        scope.WriteFile("basic/example-review/SKILL.md.template", "Review an example when requested.\n");
        var reader = new SkillSourceDefinitionReader();

        var result = await reader.ReadOneAsync(AbsolutePath.Parse(skillDirectory), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task BundledSourceContractExamples_AreAcceptedAsSourceInput ()
    {
        var contractPath = Path.Combine(
            SkillTestData.GetRepositoryRoot(),
            "skills",
            "definitions",
            "skills",
            "basic",
            "agent-distribution-packaging",
            "references",
            "source-definition-contract.md.template");
        var contract = File.ReadAllText(contractPath);
        var bundleJson = ReadJsonCodeBlock(contract, "## `bundle.json`");
        var skillJson = ReadJsonCodeBlock(contract, "## `skill.json`");
        using var scope = TestDirectories.CreateTempScope("agent-distribution-documentation", "source-contract");
        scope.WriteFile("bundle.json", bundleJson);
        var skillDirectory = scope.CreateDirectory("definitions/skills/basic/example-review");
        scope.WriteFile("definitions/skills/basic/example-review/skill.json", skillJson);
        scope.WriteFile("definitions/skills/basic/example-review/SKILL.md.template", "Review an example when requested.\n");
        var bundleReader = new AgentDistributionBundleDefinitionReader(new AgentDistributionBundleJsonSerializer());
        var sourceReader = new SkillSourceDefinitionReader();

        var bundleResult = await bundleReader.ReadAsync(AbsolutePath.Parse(scope.FullPath), CancellationToken.None);
        var sourceResult = await sourceReader.ReadOneAsync(AbsolutePath.Parse(skillDirectory), CancellationToken.None);

        Assert.True(bundleResult.IsSuccess, bundleResult.Failure?.Message);
        Assert.True(sourceResult.IsSuccess, sourceResult.Failure?.Message);
    }

    private static string ReadReadme ()
    {
        return File.ReadAllText(Path.Combine(SkillTestData.GetRepositoryRoot(), "README.md"));
    }

    private static string ReadJsonCodeBlock (
        string markdown,
        string precedingText)
    {
        var sectionStart = markdown.IndexOf(precedingText, StringComparison.Ordinal);
        Assert.True(sectionStart >= 0, $"README does not contain the expected section marker: {precedingText}");

        const string openingFence = "```json\n";
        var contentStart = markdown.IndexOf(openingFence, sectionStart, StringComparison.Ordinal);
        Assert.True(contentStart >= 0, $"README section does not contain a JSON code block: {precedingText}");
        contentStart += openingFence.Length;

        var contentEnd = markdown.IndexOf("```", contentStart, StringComparison.Ordinal);
        Assert.True(contentEnd >= 0, $"README JSON code block is not closed: {precedingText}");
        return markdown[contentStart..contentEnd];
    }
}
