using MackySoft.AgentSkills.Shared;
using MackySoft.AgentSkills.Sources;
using MackySoft.Tests;

namespace MackySoft.AgentSkills.Tests.Sources;

public sealed class SkillSourceDefinitionReaderTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAllAsync_ReadsFixtureDefinitionsFromCategoryDirectories ()
    {
        var reader = new SkillSourceDefinitionReader();

        var result = await reader.ReadAllAsync(SkillTestData.GetDefinitionsRoot(), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(SkillTestData.ExpectedSkillNames, result.Value!.Select(static definition => definition.Metadata.SkillName.Value).ToArray());
        Assert.All(result.Value!, static definition =>
        {
            Assert.Equal(SkillSourceMetadata.CurrentSchemaVersion, definition.Metadata.SchemaVersion);
            Assert.Equal(SkillTestData.ExpectedCategory, definition.Metadata.Category.Value);
            Assert.DoesNotContain("---", definition.SkillTemplate.TrimStart().Split('\n')[0], StringComparison.Ordinal);
            Assert.False(definition.SkillTemplate.TrimStart().StartsWith("# ", StringComparison.Ordinal));
            Assert.Empty(definition.Metadata.Dependencies);
            Assert.NotEmpty(definition.References);
        });
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadOneAsync_DerivesCategoryAndSkillNameFromDirectoryStructure ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "derived-category");
        var skillDirectory = WriteMinimalDefinition(scope, category: "utilities");
        var reader = new SkillSourceDefinitionReader();

        var result = await reader.ReadOneAsync(skillDirectory, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal("utilities", result.Value!.Metadata.Category.Value);
        Assert.Equal("sample-skill", result.Value.Metadata.SkillName.Value);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAllAsync_DoesNotDiscoverDefinitionsBelowSkillDepth ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "fixed-depth");
        WriteMinimalDefinition(scope);
        scope.WriteFile(
            "core/sample-skill/nested/deep-skill/skill.json",
            CreateSkillJson([]));
        var reader = new SkillSourceDefinitionReader();

        var result = await reader.ReadAllAsync(scope.FullPath, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(["sample-skill"], result.Value!.Select(static definition => definition.Metadata.SkillName.Value).ToArray());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAllAsync_RejectsLegacyFlatDefinitionLayout ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "legacy-flat-layout");
        scope.WriteFile("sample-skill/skill.json", CreateSkillJson([]));
        scope.WriteFile("sample-skill/SKILL.md.template", "Use this skill when testing source validation.\n");
        scope.WriteFile("sample-skill/references/reference.md.template", "# Reference\n");
        var reader = new SkillSourceDefinitionReader();

        var result = await reader.ReadAllAsync(scope.FullPath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.SourceInvalid, result.Failure!.Code);
        Assert.Contains("<category>/<skill>", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAllAsync_RejectsInvalidCategoryDirectoryName ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "invalid-category");
        WriteMinimalDefinition(scope, category: "Core");
        var reader = new SkillSourceDefinitionReader();

        var result = await reader.ReadAllAsync(scope.FullPath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.SourceInvalid, result.Failure!.Code);
        Assert.Contains("category", result.Failure.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAllAsync_RejectsEmptyCategory ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "empty-category");
        scope.CreateDirectory("core");
        var reader = new SkillSourceDefinitionReader();

        var result = await reader.ReadAllAsync(scope.FullPath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.SourceInvalid, result.Failure!.Code);
        Assert.Contains("does not contain any definitions", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAllAsync_RejectsDuplicateSkillDirectoryNameAcrossCategories ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "duplicate-across-categories");
        WriteMinimalDefinition(scope, category: "core", skillName: "sample-skill");
        WriteMinimalDefinition(scope, category: "utilities", skillName: "sample-skill");
        var reader = new SkillSourceDefinitionReader();

        var result = await reader.ReadAllAsync(scope.FullPath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.SourceInvalid, result.Failure!.Code);
        Assert.Contains("duplicate skill directory name across categories", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadOneAsync_RejectsUnknownSkillJsonProperty ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "unknown-property");
        var skillDirectory = WriteMinimalDefinition(scope, extraJsonProperty: ",\n  \"tier\": \"basic\"");
        var reader = new SkillSourceDefinitionReader();

        var result = await reader.ReadOneAsync(skillDirectory, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.SourceInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadOneAsync_RejectsRedundantCategoryProperty ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "redundant-category");
        var skillDirectory = WriteMinimalDefinition(scope, extraJsonProperty: ",\n  \"category\": \"core\"");
        var reader = new SkillSourceDefinitionReader();

        var result = await reader.ReadOneAsync(skillDirectory, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.SourceInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadOneAsync_RejectsRedundantSkillNameProperty ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "redundant-skill-name");
        var skillDirectory = WriteMinimalDefinition(scope, extraJsonProperty: ",\n  \"skillName\": \"sample-skill\"");
        var reader = new SkillSourceDefinitionReader();

        var result = await reader.ReadOneAsync(skillDirectory, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.SourceInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadOneAsync_RejectsRedundantReferencesProperty ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "redundant-references");
        var skillDirectory = WriteMinimalDefinition(scope, extraJsonProperty: ",\n  \"references\": [\"reference.md\"]");
        var reader = new SkillSourceDefinitionReader();

        var result = await reader.ReadOneAsync(skillDirectory, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.SourceInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadOneAsync_RejectsNonCanonicalSkillJsonPropertyOrder ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "property-order");
        var skillDirectory = scope.CreateDirectory("core/sample-skill");
        scope.WriteFile(
            "core/sample-skill/skill.json",
            """
            {
              "displayName": "Sample Skill",
              "schemaVersion": 1,
              "description": "Use when testing source validation.",
              "dependencies": []
            }
            """);
        scope.WriteFile("core/sample-skill/SKILL.md.template", "Use this skill when testing source validation.\n");
        var reader = new SkillSourceDefinitionReader();

        var result = await reader.ReadOneAsync(skillDirectory, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.SourceInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadOneAsync_RejectsMalformedSkillJson ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "malformed-json");
        var skillDirectory = scope.CreateDirectory("core/sample-skill");
        scope.WriteFile("core/sample-skill/skill.json", "{");
        scope.WriteFile("core/sample-skill/SKILL.md.template", "Use this skill when testing source validation.\n");
        var reader = new SkillSourceDefinitionReader();

        var result = await reader.ReadOneAsync(skillDirectory, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.SourceInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadOneAsync_RejectsUnsupportedSchemaVersion ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "unsupported-schema");
        var skillDirectory = WriteMinimalDefinition(scope, schemaVersion: 2);
        var reader = new SkillSourceDefinitionReader();

        var result = await reader.ReadOneAsync(skillDirectory, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.SourceInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadOneAsync_ReadsDefinitionWithoutReferences ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "no-references");
        var skillDirectory = WriteMinimalDefinition(scope, references: [], writeReferenceTemplates: false);
        var reader = new SkillSourceDefinitionReader();

        var result = await reader.ReadOneAsync(skillDirectory, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Empty(result.Value!.Metadata.References);
        Assert.Empty(result.Value.References);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadOneAsync_DerivesSortedReferencesFromDirectMarkdownTemplateFiles ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "derived-references");
        var skillDirectory = WriteMinimalDefinition(scope, references: ["z-reference.md", "a-reference.md"]);
        var reader = new SkillSourceDefinitionReader();

        var result = await reader.ReadOneAsync(skillDirectory, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(["a-reference.md", "z-reference.md"], result.Value!.Metadata.References);
        Assert.Equal(
            ["a-reference.md", "z-reference.md"],
            result.Value.References.Select(static reference => reference.FileName).ToArray());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadOneAsync_RejectsSkillTemplateWithTopLevelHeading ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "template-heading");
        var skillDirectory = WriteMinimalDefinition(scope, skillTemplate: "# Sample\n");
        var reader = new SkillSourceDefinitionReader();

        var result = await reader.ReadOneAsync(skillDirectory, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.SourceInvalid, result.Failure!.Code);
        Assert.Contains("top-level heading", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadOneAsync_ReadsSortedDependencies ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "sorted-dependencies");
        var skillDirectory = WriteMinimalDefinition(scope, dependencies: ["z-helper", "a-helper"]);
        var reader = new SkillSourceDefinitionReader();

        var result = await reader.ReadOneAsync(skillDirectory, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(["a-helper", "z-helper"], result.Value!.Metadata.Dependencies.Select(static dependency => dependency.Value).ToArray());
    }

    [Theory]
    [InlineData("")]
    [InlineData("UnsafeSkill")]
    [InlineData("../escape")]
    [InlineData("-helper")]
    [Trait("Size", "Small")]
    public async Task ReadOneAsync_RejectsInvalidDependencyLiteral (string dependency)
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "invalid-dependency");
        var skillDirectory = WriteMinimalDefinition(scope, dependencies: [dependency]);
        var reader = new SkillSourceDefinitionReader();

        var result = await reader.ReadOneAsync(skillDirectory, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.SourceInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadOneAsync_RejectsDuplicateDependency ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "duplicate-dependency");
        var skillDirectory = WriteMinimalDefinition(scope, dependencies: ["helper-skill", "helper-skill"]);
        var reader = new SkillSourceDefinitionReader();

        var result = await reader.ReadOneAsync(skillDirectory, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.SourceInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadOneAsync_RejectsSelfDependency ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "self-dependency");
        var skillDirectory = WriteMinimalDefinition(scope, dependencies: ["sample-skill"]);
        var reader = new SkillSourceDefinitionReader();

        var result = await reader.ReadOneAsync(skillDirectory, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.SourceInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAllAsync_RejectsUndefinedDependency ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "undefined-dependency");
        WriteMinimalDefinition(scope, dependencies: ["missing-helper"]);
        var reader = new SkillSourceDefinitionReader();

        var result = await reader.ReadAllAsync(scope.FullPath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.SourceInvalid, result.Failure!.Code);
        Assert.Contains("dependency was not found", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAllAsync_RejectsDependencyCycleAcrossCategories ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "dependency-cycle");
        WriteMinimalDefinition(scope, category: "core", skillName: "skill-a", dependencies: ["skill-b"]);
        WriteMinimalDefinition(scope, category: "utilities", skillName: "skill-b", dependencies: ["skill-a"]);
        var reader = new SkillSourceDefinitionReader();

        var result = await reader.ReadAllAsync(scope.FullPath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.SourceInvalid, result.Failure!.Code);
        Assert.Contains("dependency cycle", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadOneAsync_RejectsInvalidReferenceTemplateFileName ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "invalid-reference-name");
        var skillDirectory = WriteMinimalDefinition(scope, references: [], writeReferenceTemplates: false);
        scope.WriteFile("core/sample-skill/references/reference.txt.template", "Reference\n");
        var reader = new SkillSourceDefinitionReader();

        var result = await reader.ReadOneAsync(skillDirectory, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.SourceInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadOneAsync_RejectsNestedReferenceDirectory ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "nested-reference");
        var skillDirectory = WriteMinimalDefinition(scope, references: [], writeReferenceTemplates: false);
        scope.WriteFile("core/sample-skill/references/nested/reference.md.template", "Reference\n");
        var reader = new SkillSourceDefinitionReader();

        var result = await reader.ReadOneAsync(skillDirectory, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.SourceInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadOneAsync_RejectsUnsafeSkillDirectoryName ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "unsafe-skill-name");
        var skillDirectory = WriteMinimalDefinition(scope, skillName: "SampleSkill");
        var reader = new SkillSourceDefinitionReader();

        var result = await reader.ReadOneAsync(skillDirectory, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.SourceInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadOneAsync_RejectsSkillTemplateSymlinkOutsideSkillDirectory ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "template-symlink");
        using var outsideScope = TestDirectories.CreateTempScope("agent-skills-skills", "template-symlink-outside");
        var skillDirectory = WriteMinimalDefinition(scope, writeSkillTemplate: false);
        var outsideTemplate = outsideScope.WriteFile("outside-template.md", "# Outside\n");
        if (!TryCreateFileSymbolicLink(Path.Combine(skillDirectory, "SKILL.md.template"), outsideTemplate))
        {
            return;
        }

        var reader = new SkillSourceDefinitionReader();

        var result = await reader.ReadOneAsync(skillDirectory, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.SourceInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadOneAsync_RejectsReferenceTemplateSymlinkOutsideSkillDirectory ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "reference-symlink");
        using var outsideScope = TestDirectories.CreateTempScope("agent-skills-skills", "reference-symlink-outside");
        var skillDirectory = WriteMinimalDefinition(scope, writeReferenceTemplates: false);
        var outsideTemplate = outsideScope.WriteFile("outside-reference.md", "# Outside\n");
        Directory.CreateDirectory(Path.Combine(skillDirectory, "references"));
        if (!TryCreateFileSymbolicLink(Path.Combine(skillDirectory, "references", "reference.md.template"), outsideTemplate))
        {
            return;
        }

        var reader = new SkillSourceDefinitionReader();

        var result = await reader.ReadOneAsync(skillDirectory, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.SourceInvalid, result.Failure!.Code);
    }

    private static string WriteMinimalDefinition (
        TestDirectoryScope scope,
        string category = "core",
        string skillName = "sample-skill",
        IReadOnlyList<string>? dependencies = null,
        IReadOnlyList<string>? references = null,
        string extraJsonProperty = "",
        string skillTemplate = "Use this skill when testing source validation.\n",
        bool writeSkillTemplate = true,
        bool writeReferenceTemplates = true,
        int schemaVersion = SkillSourceMetadata.CurrentSchemaVersion)
    {
        dependencies ??= [];
        references ??= ["reference.md"];
        var relativeSkillDirectory = Path.Combine(category, skillName);
        var skillDirectory = scope.CreateDirectory(relativeSkillDirectory);
        scope.WriteFile(
            Path.Combine(relativeSkillDirectory, "skill.json"),
            CreateSkillJson(dependencies, extraJsonProperty, schemaVersion));

        if (writeSkillTemplate)
        {
            scope.WriteFile(Path.Combine(relativeSkillDirectory, "SKILL.md.template"), skillTemplate);
        }

        if (writeReferenceTemplates)
        {
            foreach (var reference in references.Where(static reference => !reference.Contains('/') && !reference.Contains('\\')))
            {
                scope.WriteFile(Path.Combine(relativeSkillDirectory, "references", reference + ".template"), "# Reference\n");
            }
        }

        return skillDirectory;
    }

    private static string CreateSkillJson (
        IReadOnlyList<string> dependencies,
        string extraJsonProperty = "",
        int schemaVersion = SkillSourceMetadata.CurrentSchemaVersion)
    {
        return $$"""
            {
              "schemaVersion": {{schemaVersion}},
              "displayName": "Sample Skill",
              "description": "Use when testing source validation.",
              "dependencies": {{SerializeArray(dependencies)}}{{extraJsonProperty}}
            }
            """;
    }

    private static string SerializeArray (IReadOnlyList<string> values)
    {
        return values.Count == 0
            ? "[]"
            : "[\n" + string.Join(",\n", values.Select(static value => $"    \"{value}\"")) + "\n  ]";
    }

    private static bool TryCreateFileSymbolicLink (
        string linkPath,
        string targetPath)
    {
        try
        {
            File.CreateSymbolicLink(linkPath, targetPath);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
