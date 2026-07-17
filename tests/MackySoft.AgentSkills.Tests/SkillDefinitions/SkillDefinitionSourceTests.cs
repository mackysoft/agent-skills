using System.Text.Json;

namespace MackySoft.AgentSkills.Tests.SkillDefinitions;

public sealed class SkillDefinitionSourceTests
{
    private const UnixFileMode ExecutableFileModes =
        UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;

    private static readonly string[] ExpectedBundleJsonProperties =
    [
        "schemaVersion",
        "catalogId",
        "skillBundleVersion",
    ];

    private static readonly string[] ExpectedSkillJsonProperties =
    [
        "schemaVersion",
        "displayName",
        "description",
        "dependencies",
    ];

    private static readonly string[] ForbiddenCanonicalSchemaTerms =
    [
        "argsSchema",
        "resultSchema",
    ];

    private static readonly string[] ForbiddenDangerousWorkflowExamples =
    [
        "agent-skills call --allowDangerous",
        "agent-skills plan --allowDangerous",
        "agent-skills validate --allowDangerous",
        "arbitrary C#",
        "execute C#",
        "run C#",
        "arbitrary shell",
        "execute shell",
        "run shell",
        "edit Unity YAML directly",
        "modify Unity YAML directly",
        "write Unity YAML directly",
    ];

    [Fact]
    [Trait("Size", "Small")]
    public void SkillBundle_HasExpectedSourceMetadata ()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(SkillTestData.GetSkillBundleRoot(), "bundle.json")));
        var root = document.RootElement;

        Assert.Equal(ExpectedBundleJsonProperties, root.EnumerateObject().Select(static property => property.Name).ToArray());
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("com.mackysoft.agent-skills", root.GetProperty("catalogId").GetString());
        Assert.Equal(1, root.GetProperty("skillBundleVersion").GetInt32());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void SkillDefinitions_HaveExpectedCategoryAndSourceMetadata ()
    {
        var definitionsRoot = SkillTestData.GetDefinitionsRoot();
        var categories = Directory.GetDirectories(definitionsRoot)
            .Select(Path.GetFileName)
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Select(static name => name!)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal([SkillTestData.ExpectedCategory], categories);

        var categoryRoot = Path.Combine(definitionsRoot, SkillTestData.ExpectedCategory);
        var skillNames = Directory.GetDirectories(categoryRoot)
            .Select(Path.GetFileName)
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Select(static name => name!)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(SkillTestData.ExpectedSkillNames, skillNames);

        foreach (var skillName in SkillTestData.ExpectedSkillNames)
        {
            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(categoryRoot, skillName, "skill.json")));
            var root = document.RootElement;

            Assert.Equal(ExpectedSkillJsonProperties, root.EnumerateObject().Select(static property => property.Name).ToArray());
            Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
            Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("displayName").GetString()));
            Assert.Empty(root.GetProperty("dependencies").EnumerateArray());

            var description = root.GetProperty("description").GetString();
            Assert.False(string.IsNullOrWhiteSpace(description));
            Assert.InRange(description.Length, 1, 1024);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void SkillDefinitions_HaveExistingReferenceTemplates ()
    {
        var definitionsRoot = SkillTestData.GetDefinitionsRoot();

        foreach (var skillName in SkillTestData.ExpectedSkillNames)
        {
            var skillDirectory = Path.Combine(definitionsRoot, SkillTestData.ExpectedCategory, skillName);
            var references = GetReferenceTemplatePaths(skillDirectory);

            Assert.NotEmpty(references);

            foreach (var referencePath in references)
            {
                var reference = Path.GetFileName(referencePath);
                Assert.EndsWith(".md.template", reference, StringComparison.Ordinal);
                Assert.DoesNotContain("/", reference, StringComparison.Ordinal);
                Assert.DoesNotContain("\\", reference, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void SkillDefinitions_KeepInstructionGuardrails ()
    {
        var definitionsRoot = SkillTestData.GetDefinitionsRoot();

        foreach (var skillName in SkillTestData.ExpectedSkillNames)
        {
            var skillDirectory = Path.Combine(definitionsRoot, SkillTestData.ExpectedCategory, skillName);
            var template = File.ReadAllText(Path.Combine(skillDirectory, "SKILL.md.template"));

            Assert.False(template.TrimStart().StartsWith("---", StringComparison.Ordinal));
            Assert.False(template.TrimStart().StartsWith("# ", StringComparison.Ordinal));
            Assert.InRange(CountLogicalLines(template), 1, 499);
            Assert.Contains("agent-skills ops describe <opName>", template, StringComparison.Ordinal);
            Assert.Contains("read -> describe -> build request -> validate -> plan -> call -> verify", template, StringComparison.Ordinal);
            Assert.Contains("fixed sleep", template, StringComparison.Ordinal);
            Assert.Contains("IPC_TIMEOUT", template, StringComparison.Ordinal);
            Assert.Contains("payload.opResults[].applied", template, StringComparison.Ordinal);
            Assert.Contains("changed", template, StringComparison.Ordinal);
            Assert.Contains("touched", template, StringComparison.Ordinal);
            Assert.Contains("readPostcondition", template, StringComparison.Ordinal);
            Assert.Contains("--allowDangerous", template, StringComparison.Ordinal);

            foreach (var forbiddenTerm in ForbiddenCanonicalSchemaTerms)
            {
                Assert.DoesNotContain(forbiddenTerm, template, StringComparison.OrdinalIgnoreCase);
            }

            foreach (var forbiddenTerm in ForbiddenDangerousWorkflowExamples)
            {
                Assert.DoesNotContain(forbiddenTerm, template, StringComparison.OrdinalIgnoreCase);
            }

            Assert.False(ContainsOperationCatalogTableCopy(template), skillName);
            Assert.False(Directory.Exists(Path.Combine(skillDirectory, "scripts")), skillName);
            Assert.False(Directory.Exists(Path.Combine(skillDirectory, "assets")), skillName);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void SkillDefinitions_KeepReferenceTemplatesBoundedAndNonCanonical ()
    {
        var definitionsRoot = SkillTestData.GetDefinitionsRoot();

        foreach (var skillName in SkillTestData.ExpectedSkillNames)
        {
            var skillDirectory = Path.Combine(definitionsRoot, SkillTestData.ExpectedCategory, skillName);
            var referencePaths = GetReferenceTemplatePaths(skillDirectory);

            foreach (var referencePath in referencePaths)
            {
                var template = File.ReadAllText(referencePath);

                Assert.InRange(CountLogicalLines(template), 1, 999);

                foreach (var forbiddenTerm in ForbiddenCanonicalSchemaTerms)
                {
                    Assert.DoesNotContain(forbiddenTerm, template, StringComparison.OrdinalIgnoreCase);
                }

                foreach (var forbiddenTerm in ForbiddenDangerousWorkflowExamples)
                {
                    Assert.DoesNotContain(forbiddenTerm, template, StringComparison.OrdinalIgnoreCase);
                }

                Assert.False(ContainsOperationCatalogTableCopy(template), Path.GetFileName(referencePath));
            }
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void SkillDefinitions_DoNotIncludeExecutableFiles ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var definitionsRoot = SkillTestData.GetDefinitionsRoot();

        foreach (var path in Directory.EnumerateFiles(definitionsRoot, "*", SearchOption.AllDirectories))
        {
            Assert.Equal((UnixFileMode)0, File.GetUnixFileMode(path) & ExecutableFileModes);
        }
    }

    private static int CountLogicalLines (string text)
    {
        var lineCount = 0;
        using var reader = new StringReader(text);

        while (reader.ReadLine() is not null)
        {
            lineCount++;
        }

        return lineCount;
    }

    private static IReadOnlyList<string> GetReferenceTemplatePaths (string skillDirectory)
    {
        var referencesRoot = Path.Combine(skillDirectory, "references");
        return Directory.GetFiles(referencesRoot, "*.md.template", SearchOption.TopDirectoryOnly)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static bool ContainsOperationCatalogTableCopy (string text)
    {
        var operationTableRows = 0;
        using var reader = new StringReader(text);

        while (reader.ReadLine() is { } line)
        {
            if (line.TrimStart().StartsWith("|", StringComparison.Ordinal)
                && line.Contains("agent-skills.", StringComparison.OrdinalIgnoreCase))
            {
                operationTableRows++;
            }
        }

        return operationTableRows >= 3;
    }
}
