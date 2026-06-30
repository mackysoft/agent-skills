using MackySoft.AgentSkills.Shared;
using MackySoft.AgentSkills.Sources;
using MackySoft.Tests;

namespace MackySoft.AgentSkills.Tests.Sources;

public sealed class SkillSourceDefinitionReaderTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAllAsync_ReadsFixtureDefinitions ()
    {
        var reader = new SkillSourceDefinitionReader();

        var result = await reader.ReadAllAsync(SkillTestData.GetDefinitionsRoot(), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(SkillTestData.ExpectedSkillNames, result.Value!.Select(static definition => definition.Metadata.SkillName.Value).ToArray());
        Assert.All(result.Value!, static definition =>
        {
            Assert.DoesNotContain("---", definition.SkillTemplate.TrimStart().Split('\n')[0], StringComparison.Ordinal);
            Assert.False(definition.SkillTemplate.TrimStart().StartsWith("# ", StringComparison.Ordinal));
            Assert.Empty(definition.Metadata.Dependencies);
            Assert.NotEmpty(definition.References);
        });
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadOneAsync_Fails_WhenSkillJsonHasUnknownProperty ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "unknown-property");
        var skillDirectory = WriteMinimalDefinition(scope, extraJsonProperty: ",\n  \"hostAllowlist\": [\"openai\"]");
        var reader = new SkillSourceDefinitionReader();

        var result = await reader.ReadOneAsync(skillDirectory, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.SourceInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadOneAsync_Fails_WhenSkillJsonIsMalformed ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "malformed-json");
        var skillDirectory = scope.CreateDirectory("sample-skill");
        scope.WriteFile("sample-skill/skill.json", "{");
        scope.WriteFile("sample-skill/SKILL.md.template", "Use this skill when testing source validation.\n");
        var reader = new SkillSourceDefinitionReader();

        var result = await reader.ReadOneAsync(skillDirectory, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.SourceInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadOneAsync_Fails_WhenTierIsMissing ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "missing-tier");
        var skillDirectory = scope.CreateDirectory("sample-skill");
        scope.WriteFile(
            "sample-skill/skill.json",
            """
            {
              "schemaVersion": 1,
              "skillName": "sample-skill",
              "displayName": "Sample Skill",
              "description": "Use when testing source validation.",
              "dependencies": [],
              "references": [
                "reference.md"
              ]
            }
            """);
        scope.WriteFile("sample-skill/SKILL.md.template", "Use this skill when testing source validation.\n");
        scope.WriteFile("sample-skill/references/reference.md.template", "# Reference\n");
        var reader = new SkillSourceDefinitionReader();

        var result = await reader.ReadOneAsync(skillDirectory, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.SourceInvalid, result.Failure!.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Tier")]
    [InlineData("basic_tier")]
    [InlineData("-basic")]
    [Trait("Size", "Small")]
    public async Task ReadOneAsync_Fails_WhenTierLiteralIsInvalid (string tier)
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "invalid-tier");
        var skillDirectory = WriteMinimalDefinition(scope, tier: tier);
        var reader = new SkillSourceDefinitionReader();

        var result = await reader.ReadOneAsync(skillDirectory, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.SourceInvalid, result.Failure!.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Com.MackySoft.AgentSkills")]
    [InlineData("com..mackysoft")]
    [InlineData("com.mackysoft.")]
    [InlineData("com.-mackysoft")]
    [InlineData("com.mackysoft-")]
    [Trait("Size", "Small")]
    public async Task ReadOneAsync_Fails_WhenCatalogIdLiteralIsInvalid (string catalogId)
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "invalid-catalog-id");
        var skillDirectory = WriteMinimalDefinition(scope, catalogId: catalogId);
        var reader = new SkillSourceDefinitionReader();

        var result = await reader.ReadOneAsync(skillDirectory, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.SourceInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadOneAsync_Fails_WhenTierPropertyOrderIsNotCanonical ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "tier-order");
        var skillDirectory = scope.CreateDirectory("sample-skill");
        scope.WriteFile(
            "sample-skill/skill.json",
            """
            {
              "schemaVersion": 1,
              "tier": "basic",
              "catalogId": "com.mackysoft.agent-skills",
              "skillName": "sample-skill",
              "displayName": "Sample Skill",
              "description": "Use when testing source validation.",
              "dependencies": [],
              "references": [
                "reference.md"
              ]
            }
            """);
        scope.WriteFile("sample-skill/SKILL.md.template", "Use this skill when testing source validation.\n");
        scope.WriteFile("sample-skill/references/reference.md.template", "# Reference\n");
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
        var skillDirectory = scope.CreateDirectory("sample-skill");
        scope.WriteFile(
            "sample-skill/skill.json",
            """
            {
              "schemaVersion": 1,
              "catalogId": "com.mackysoft.agent-skills",
              "tier": "basic",
              "skillName": "sample-skill",
              "displayName": "Sample Skill",
              "description": "Use when testing source validation.",
              "dependencies": [],
              "references": []
            }
            """);
        scope.WriteFile("sample-skill/SKILL.md.template", "Use this skill when testing source validation.\n");
        var reader = new SkillSourceDefinitionReader();

        var result = await reader.ReadOneAsync(skillDirectory, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Empty(result.Value!.Metadata.References);
        Assert.Empty(result.Value.References);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadOneAsync_Fails_WhenSkillTemplateContainsTopLevelHeading ()
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
    public async Task ReadOneAsync_ReadsDefinitionWithSortedDependencies ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "with-dependencies");
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
    public async Task ReadOneAsync_Fails_WhenDependencyLiteralIsInvalid (string dependency)
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
    public async Task ReadOneAsync_Fails_WhenDependencyIsDuplicated ()
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
    public async Task ReadOneAsync_Fails_WhenDependencyReferencesSelf ()
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
    public async Task ReadAllAsync_Fails_WhenDependencyIsUndefined ()
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
    public async Task ReadAllAsync_Fails_WhenDependencyCycleExists ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "dependency-cycle");
        WriteMinimalDefinition(scope, skillName: "skill-a", dependencies: ["skill-b"]);
        WriteMinimalDefinition(scope, skillName: "skill-b", dependencies: ["skill-a"]);
        var reader = new SkillSourceDefinitionReader();

        var result = await reader.ReadAllAsync(scope.FullPath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.SourceInvalid, result.Failure!.Code);
        Assert.Contains("dependency cycle", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadOneAsync_Fails_WhenReferenceEscapesDirectory ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "reference-traversal");
        var skillDirectory = scope.CreateDirectory("sample-skill");
        scope.WriteFile(
            "sample-skill/skill.json",
            """
            {
              "schemaVersion": 1,
              "catalogId": "com.mackysoft.agent-skills",
              "tier": "basic",
              "skillName": "sample-skill",
              "displayName": "Sample Skill",
              "description": "Use when testing source validation.",
              "dependencies": [],
              "references": [
                "../escape.md"
              ]
            }
            """);
        scope.WriteFile("sample-skill/SKILL.md.template", "Use this skill when testing source validation.\n");
        var reader = new SkillSourceDefinitionReader();

        var result = await reader.ReadOneAsync(skillDirectory, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.SourceInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadOneAsync_Fails_WhenReferenceTemplateIsMissing ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "missing-reference");
        var skillDirectory = WriteMinimalDefinition(scope, writeReferenceTemplate: false);
        var reader = new SkillSourceDefinitionReader();

        var result = await reader.ReadOneAsync(skillDirectory, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.SourceInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadOneAsync_Fails_WhenSkillNameIsNotSafeIdentifier ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "unsafe-skill-name");
        var skillDirectory = scope.CreateDirectory("SampleSkill");
        scope.WriteFile(
            "SampleSkill/skill.json",
            """
            {
              "schemaVersion": 1,
              "catalogId": "com.mackysoft.agent-skills",
              "tier": "basic",
              "skillName": "SampleSkill",
              "displayName": "Sample Skill",
              "description": "Use when testing source validation.",
              "dependencies": [],
              "references": [
                "reference.md"
              ]
            }
            """);
        scope.WriteFile("SampleSkill/SKILL.md.template", "Use this skill when testing source validation.\n");
        scope.WriteFile("SampleSkill/references/reference.md.template", "# Reference\n");
        var reader = new SkillSourceDefinitionReader();

        var result = await reader.ReadOneAsync(skillDirectory, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.SourceInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadOneAsync_Fails_WhenSkillTemplateSymlinkEscapesSkillDirectory ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "template-symlink");
        using var outsideScope = TestDirectories.CreateTempScope("agent-skills-skills", "template-symlink-outside");
        var skillDirectory = WriteMinimalDefinition(scope, writeSkillTemplate: false);
        var outsideTemplate = outsideScope.WriteFile("outside-template.md", "# Outside\n");
        try
        {
            File.CreateSymbolicLink(Path.Combine(skillDirectory, "SKILL.md.template"), outsideTemplate);
        }
        catch (IOException)
        {
            return;
        }
        catch (UnauthorizedAccessException)
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
    public async Task ReadOneAsync_Fails_WhenReferenceTemplateSymlinkEscapesSkillDirectory ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "reference-symlink");
        using var outsideScope = TestDirectories.CreateTempScope("agent-skills-skills", "reference-symlink-outside");
        var skillDirectory = WriteMinimalDefinition(scope, writeReferenceTemplate: false);
        var outsideTemplate = outsideScope.WriteFile("outside-reference.md", "# Outside\n");
        Directory.CreateDirectory(Path.Combine(skillDirectory, "references"));
        try
        {
            File.CreateSymbolicLink(Path.Combine(skillDirectory, "references", "reference.md.template"), outsideTemplate);
        }
        catch (IOException)
        {
            return;
        }
        catch (UnauthorizedAccessException)
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
        string extraJsonProperty = "",
        string tier = "basic",
        string catalogId = "com.mackysoft.agent-skills",
        string skillName = "sample-skill",
        IReadOnlyList<string>? dependencies = null,
        string skillTemplate = "Use this skill when testing source validation.\n",
        bool writeSkillTemplate = true,
        bool writeReferenceTemplate = true)
    {
        dependencies ??= [];
        var dependencyJson = dependencies.Count == 0
            ? "[]"
            : "[\n" + string.Join(",\n", dependencies.Select(static dependency => $"    \"{dependency}\"")) + "\n  ]";
        var skillDirectory = scope.CreateDirectory(skillName);
        scope.WriteFile(
            $"{skillName}/skill.json",
            $$"""
            {
              "schemaVersion": 1,
              "catalogId": "{{catalogId}}",
              "tier": "{{tier}}",
              "skillName": "{{skillName}}",
              "displayName": "Sample Skill",
              "description": "Use when testing source validation.",
              "dependencies": {{dependencyJson}},
              "references": [
                "reference.md"
              ]{{extraJsonProperty}}
            }
            """);
        if (writeSkillTemplate)
        {
            scope.WriteFile($"{skillName}/SKILL.md.template", skillTemplate);
        }

        if (writeReferenceTemplate)
        {
            scope.WriteFile($"{skillName}/references/reference.md.template", "# Reference\n");
        }

        return skillDirectory;
    }
}
