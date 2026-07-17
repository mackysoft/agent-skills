using System.Text.Json;
using MackySoft.AgentSkills.Bundles;
using MackySoft.AgentSkills.Catalogs;
using MackySoft.AgentSkills.Digests;

namespace MackySoft.AgentSkills.Tests.Bundles;

public sealed class SkillBundleJsonSerializerTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void SerializeDefinitionAndDescriptor_UseDistinctCanonicalFieldSets ()
    {
        var serializer = new SkillBundleJsonSerializer();
        var definition = new SkillBundleDefinition(
            SkillBundleDefinition.CurrentSchemaVersion,
            new SkillCatalogId("com.mackysoft.agent-skills"),
            7);
        var descriptor = new SkillBundleDescriptor(
            definition.SchemaVersion,
            definition.CatalogId,
            definition.SkillBundleVersion,
            Sha256Digest.Parse(new string('a', 64)));

        var definitionJson = serializer.SerializeDefinition(definition);
        var descriptorJson = serializer.SerializeDescriptor(descriptor);

        using var definitionDocument = JsonDocument.Parse(definitionJson);
        using var descriptorDocument = JsonDocument.Parse(descriptorJson);
        Assert.Equal(
            ["schemaVersion", "catalogId", "skillBundleVersion"],
            definitionDocument.RootElement.EnumerateObject().Select(static property => property.Name).ToArray());
        Assert.Equal(
            ["schemaVersion", "catalogId", "skillBundleVersion", "bundleDigest"],
            descriptorDocument.RootElement.EnumerateObject().Select(static property => property.Name).ToArray());
        Assert.Equal(new string('a', 64), descriptorDocument.RootElement.GetProperty("bundleDigest").GetString());
        Assert.DoesNotContain('\r', definitionJson);
        Assert.EndsWith("\n", definitionJson, StringComparison.Ordinal);
        Assert.DoesNotContain('\r', descriptorJson);
        Assert.EndsWith("\n", descriptorJson, StringComparison.Ordinal);
        var deserializedDefinition = serializer.DeserializeDefinition(definitionJson);
        Assert.Equal(definition.SchemaVersion, deserializedDefinition.SchemaVersion);
        Assert.Equal(definition.CatalogId, deserializedDefinition.CatalogId);
        Assert.Equal(definition.SkillBundleVersion, deserializedDefinition.SkillBundleVersion);

        var deserializedDescriptor = serializer.DeserializeDescriptor(descriptorJson);
        Assert.Equal(descriptor.SchemaVersion, deserializedDescriptor.SchemaVersion);
        Assert.Equal(descriptor.CatalogId, deserializedDescriptor.CatalogId);
        Assert.Equal(descriptor.SkillBundleVersion, deserializedDescriptor.SkillBundleVersion);
        Assert.Equal(descriptor.BundleDigest, deserializedDescriptor.BundleDigest);
    }
}
