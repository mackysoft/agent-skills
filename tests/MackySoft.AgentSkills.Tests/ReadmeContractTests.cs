using System.Text.Json;
using MackySoft.AgentSkills.Bundles;
using MackySoft.AgentSkills.Hosts.Contracts;
using MackySoft.AgentSkills.Shared.Text;
using MackySoft.AgentSkills.Sources;
using MackySoft.Tests;

namespace MackySoft.AgentSkills.Tests;

public sealed class ReadmeContractTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task SourceBundleExample_IsAcceptedAsCanonicalInput ()
    {
        var readme = ReadReadme();
        var bundleJson = ReadJsonCodeBlock(readme, "### Define Source Skills");
        using var scope = TestDirectories.CreateTempScope("agent-skills-readme", "bundle-json");
        scope.WriteFile("bundle.json", bundleJson);
        var reader = new SkillBundleDefinitionReader(new SkillBundleJsonSerializer());

        var result = await reader.ReadAsync(scope.FullPath, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SourceSkillExample_IsAcceptedAsSourceMetadata ()
    {
        var readme = ReadReadme();
        var skillJson = ReadJsonCodeBlock(readme, "For each skill, create");
        using var scope = TestDirectories.CreateTempScope("agent-skills-readme", "skill-json");
        var skillDirectory = scope.CreateDirectory("basic/example-review");
        scope.WriteFile("basic/example-review/skill.json", skillJson);
        scope.WriteFile("basic/example-review/SKILL.md.template", "Review an example when requested.\n");
        var reader = new SkillSourceDefinitionReader();

        var result = await reader.ReadOneAsync(skillDirectory, CancellationToken.None);

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
            "basic",
            "agent-skills-packaging",
            "references",
            "source-definition-contract.md.template");
        var contract = File.ReadAllText(contractPath);
        var bundleJson = ReadJsonCodeBlock(contract, "## `bundle.json`");
        var skillJson = ReadJsonCodeBlock(contract, "## `skill.json`");
        using var scope = TestDirectories.CreateTempScope("agent-skills-documentation", "source-contract");
        scope.WriteFile("bundle.json", bundleJson);
        var skillDirectory = scope.CreateDirectory("definitions/basic/example-review");
        scope.WriteFile("definitions/basic/example-review/skill.json", skillJson);
        scope.WriteFile("definitions/basic/example-review/SKILL.md.template", "Review an example when requested.\n");
        var bundleReader = new SkillBundleDefinitionReader(new SkillBundleJsonSerializer());
        var sourceReader = new SkillSourceDefinitionReader();

        var bundleResult = await bundleReader.ReadAsync(scope.FullPath, CancellationToken.None);
        var sourceResult = await sourceReader.ReadOneAsync(skillDirectory, CancellationToken.None);

        Assert.True(bundleResult.IsSuccess, bundleResult.Failure?.Message);
        Assert.True(sourceResult.IsSuccess, sourceResult.Failure?.Message);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void SourceSchemaProjections_MatchBundledSourceContractPropertySets ()
    {
        var readme = ReadReadme();
        var contract = ReadBundledSourceContract();

        Assert.Equal(
            ReadDocumentedProperties(contract, "## `bundle.json`"),
            ReadDocumentedProperties(readme, "Create `bundle.json`"));
        Assert.Equal(
            ReadDocumentedProperties(contract, "## `skill.json`"),
            ReadDocumentedProperties(readme, "For each skill, create"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void GeneratedBundleSummary_MatchesCanonicalGeneratedBundleShape ()
    {
        var readme = ReadReadme();
        var bundlePath = Path.Combine(SkillTestData.GetRepositoryRoot(), "skills", "generated", "bundle.json");
        using var bundle = JsonDocument.Parse(File.ReadAllText(bundlePath));
        var bundleProperties = bundle.RootElement.EnumerateObject().Select(static property => property.Name).ToArray();
        var documentedProperties = ReadDocumentedProperties(readme, "The generated root `bundle.json`");

        Assert.Equal(bundleProperties, documentedProperties);
        Assert.True(IsCanonicalDigest(bundle.RootElement.GetProperty("bundleDigest").GetString()));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void GeneratedManifestSummary_MatchesCanonicalGeneratedManifestShape ()
    {
        var readme = ReadReadme();
        var generatedRoot = Path.Combine(
            SkillTestData.GetRepositoryRoot(),
            "skills",
            "generated");
        var manifestPaths = Directory.GetFiles(generatedRoot, "agent-skill.json", SearchOption.AllDirectories);
        var documentedProperties = ReadDocumentedProperties(readme, "Each `<skill-name>/agent-skill.json`");
        var documentedSection = ReadSection(readme, "### Generated Package Metadata");
        var supportedHosts = ContractLiteralCodec.GetLiterals<SkillHostKind>().ToArray();

        Assert.NotEmpty(manifestPaths);
        foreach (var manifestPath in manifestPaths)
        {
            using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
            var manifestProperties = manifest.RootElement.EnumerateObject().Select(static property => property.Name).ToArray();
            var hostArtifacts = manifest.RootElement.GetProperty("hostArtifacts").EnumerateArray().ToArray();
            var hosts = hostArtifacts.Select(static artifact => artifact.GetProperty("host").GetString()!).ToArray();

            Assert.Equal(manifestProperties, documentedProperties);
            Assert.Equal(supportedHosts, hosts);
            Assert.True(IsCanonicalDigest(manifest.RootElement.GetProperty("contentDigest").GetString()));
            Assert.True(IsCanonicalDigest(manifest.RootElement.GetProperty("manifestDigest").GetString()));

            foreach (var artifact in hostArtifacts)
            {
                var hasPath = artifact.TryGetProperty("path", out _);
                var hasDigest = artifact.TryGetProperty("digest", out var digest);
                Assert.Equal(hasPath, hasDigest);
                if (hasDigest)
                {
                    Assert.True(IsCanonicalDigest(digest.GetString()));
                }

                Assert.True(IsCanonicalDigest(artifact.GetProperty("materializedFrontmatterDigest").GetString()));
                Assert.All(
                    artifact.EnumerateObject(),
                    property => Assert.Contains($"`{property.Name}`", documentedSection, StringComparison.Ordinal));
            }
        }
    }

    private static string ReadReadme ()
    {
        return File.ReadAllText(Path.Combine(SkillTestData.GetRepositoryRoot(), "README.md"));
    }

    private static string ReadBundledSourceContract ()
    {
        return File.ReadAllText(Path.Combine(
            SkillTestData.GetRepositoryRoot(),
            "skills",
            "definitions",
            "basic",
            "agent-skills-packaging",
            "references",
            "source-definition-contract.md.template"));
    }

    private static IReadOnlyList<string> ReadDocumentedProperties (
        string markdown,
        string precedingText)
    {
        var sectionStart = markdown.IndexOf(precedingText, StringComparison.Ordinal);
        Assert.True(sectionStart >= 0, $"Documentation does not contain the expected section marker: {precedingText}");

        var properties = new List<string>();
        using var reader = new StringReader(markdown[sectionStart..]);
        while (reader.ReadLine() is { } line)
        {
            if (line.StartsWith("| `", StringComparison.Ordinal))
            {
                var propertyEnd = line.IndexOf('`', 3);
                Assert.True(propertyEnd > 3, $"Documentation contains an invalid property row: {line}");
                properties.Add(line[3..propertyEnd]);
                continue;
            }

            if (properties.Count > 0 && !line.StartsWith('|'))
            {
                break;
            }
        }

        Assert.NotEmpty(properties);
        return properties;
    }

    private static string ReadSection (
        string markdown,
        string heading)
    {
        var sectionStart = markdown.IndexOf(heading, StringComparison.Ordinal);
        Assert.True(sectionStart >= 0, $"Documentation does not contain the expected heading: {heading}");
        var sectionEnd = markdown.IndexOf("\n### ", sectionStart + heading.Length, StringComparison.Ordinal);
        return sectionEnd < 0 ? markdown[sectionStart..] : markdown[sectionStart..sectionEnd];
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

    private static bool IsCanonicalDigest (string? value)
    {
        return value is { Length: 64 }
            && value.All(static character => character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));
    }
}
