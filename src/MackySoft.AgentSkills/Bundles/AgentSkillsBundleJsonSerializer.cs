using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using MackySoft.AgentSkills.Catalogs;
using MackySoft.AgentSkills.Digests;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Bundles;

/// <summary> Serializes canonical v2 source and generated bundle descriptors. </summary>
public sealed class AgentSkillsBundleJsonSerializer
{
    private static readonly JsonWriterOptions WriterOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, Indented = true };

    /// <summary> Serializes the authored v2 definition. </summary>
    public string SerializeDefinition (AgentSkillsBundleDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return Serialize(writer => WriteShared(writer, definition.SchemaVersion, definition.CatalogId, definition.BundleVersion));
    }

    /// <summary> Serializes the generated v2 descriptor. </summary>
    public string SerializeDescriptor (AgentSkillsBundleDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return Serialize(writer =>
        {
            WriteShared(writer, descriptor.SchemaVersion, descriptor.CatalogId, descriptor.BundleVersion);
            writer.WriteString("bundleDigest", descriptor.BundleDigest.ToString());
        });
    }

    /// <summary> Deserializes an authored v2 definition. </summary>
    public AgentSkillsBundleDefinition DeserializeDefinition (string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        return new AgentSkillsBundleDefinition(root.GetProperty("schemaVersion").GetInt32(), new SkillCatalogId(root.GetProperty("catalogId").GetString() ?? string.Empty), new AgentSkillsBundleVersion(root.GetProperty("bundleVersion").GetInt32()));
    }

    /// <summary> Deserializes a generated v2 descriptor. </summary>
    public AgentSkillsBundleDescriptor DeserializeDescriptor (string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        return new AgentSkillsBundleDescriptor(root.GetProperty("schemaVersion").GetInt32(), new SkillCatalogId(root.GetProperty("catalogId").GetString() ?? string.Empty), new AgentSkillsBundleVersion(root.GetProperty("bundleVersion").GetInt32()), Sha256Digest.Parse(root.GetProperty("bundleDigest").GetString() ?? string.Empty));
    }

    private static string Serialize (Action<Utf8JsonWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, WriterOptions))
        {
            writer.WriteStartObject();
            write(writer);
            writer.WriteEndObject();
        }

        var result = SkillTextNormalizer.NormalizeToLf(Encoding.UTF8.GetString(stream.ToArray()));
        return result.EndsWith('\n') ? result : result + "\n";
    }

    private static void WriteShared (Utf8JsonWriter writer, int schemaVersion, SkillCatalogId catalogId, AgentSkillsBundleVersion bundleVersion)
    {
        writer.WriteNumber("schemaVersion", schemaVersion);
        writer.WriteString("catalogId", catalogId.Value);
        writer.WriteNumber("bundleVersion", bundleVersion.Value);
    }
}
