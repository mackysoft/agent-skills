using MackySoft.AgentSkills.Categories;
using MackySoft.AgentSkills.Names;
using MackySoft.AgentSkills.Sources;

namespace MackySoft.AgentSkills.Tests.Sources;

public sealed class SkillSourceModelTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Constructors_CaptureImmutableCollectionSnapshots ()
    {
        var dependencies = new List<SkillName> { new("helper-skill") };
        var referenceNames = new List<string> { "reference.md" };
        var metadata = CreateMetadata(dependencies, referenceNames);
        var referenceTemplates = new List<SkillSourceReference> { new("reference.md", "Reference content.\n") };
        var definition = new SkillSourceDefinition(metadata, "Skill content.\n", referenceTemplates);

        dependencies.Clear();
        referenceNames.Clear();
        referenceTemplates.Clear();

        Assert.Equal(["helper-skill"], metadata.Dependencies.Select(static dependency => dependency.Value).ToArray());
        Assert.Equal(["reference.md"], metadata.References);
        Assert.Equal(["reference.md"], definition.References.Select(static reference => reference.FileName).ToArray());
    }

    [Theory]
    [InlineData(2, "Sample Skill", "Use this sample skill.")]
    [InlineData(1, "", "Use this sample skill.")]
    [InlineData(1, "Sample Skill", "")]
    [Trait("Size", "Small")]
    public void MetadataConstructor_RejectsInvalidScalarState (
        int schemaVersion,
        string displayName,
        string description)
    {
        Assert.ThrowsAny<ArgumentException>(() => new SkillSourceMetadata(
            schemaVersion,
            new SkillCategory("core"),
            new SkillName("sample-skill"),
            displayName,
            description,
            [],
            []));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void MetadataConstructor_RejectsDescriptionLongerThanSchemaLimit ()
    {
        Assert.Throws<ArgumentException>(() => new SkillSourceMetadata(
            SkillSourceMetadata.CurrentSchemaVersion,
            new SkillCategory("core"),
            new SkillName("sample-skill"),
            "Sample Skill",
            new string('x', 1025),
            [],
            []));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void MetadataConstructor_RejectsInvalidDependencies ()
    {
        var skillName = new SkillName("sample-skill");

        Assert.Throws<ArgumentException>(() => CreateMetadata([null!], []));
        Assert.Throws<ArgumentException>(() => CreateMetadata([skillName], []));
        Assert.Throws<ArgumentException>(() => CreateMetadata([new SkillName("helper-skill"), new SkillName("helper-skill")], []));
    }

    [Theory]
    [InlineData("")]
    [InlineData("reference.txt")]
    [InlineData("../reference.md")]
    [Trait("Size", "Small")]
    public void ReferenceConstructors_RejectUnsafeFileName (string fileName)
    {
        Assert.Throws<ArgumentException>(() => CreateMetadata([], [fileName]));
        Assert.Throws<ArgumentException>(() => new SkillSourceReference(fileName, "Reference content.\n"));
    }

    [Theory]
    [InlineData("---\nname: sample-skill\n---\n")]
    [InlineData("# Sample Skill\n")]
    [Trait("Size", "Small")]
    public void DefinitionConstructor_RejectsInvalidSkillTemplate (string skillTemplate)
    {
        Assert.Throws<ArgumentException>(() => new SkillSourceDefinition(CreateMetadata([], []), skillTemplate, []));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void DefinitionConstructor_RejectsReferencesThatDoNotMatchMetadata ()
    {
        var metadata = CreateMetadata([], ["reference.md"]);

        Assert.Throws<ArgumentException>(() => new SkillSourceDefinition(metadata, "Skill content.\n", []));
    }

    private static SkillSourceMetadata CreateMetadata (
        IReadOnlyList<SkillName> dependencies,
        IReadOnlyList<string> references)
    {
        return new SkillSourceMetadata(
            SkillSourceMetadata.CurrentSchemaVersion,
            new SkillCategory("core"),
            new SkillName("sample-skill"),
            "Sample Skill",
            "Use this sample skill.",
            dependencies,
            references);
    }
}
