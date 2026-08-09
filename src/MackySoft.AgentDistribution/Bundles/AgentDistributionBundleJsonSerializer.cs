using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using MackySoft.AgentDistribution.Catalogs;
using MackySoft.AgentDistribution.Digests;
using MackySoft.AgentDistribution.Shared;

namespace MackySoft.AgentDistribution.Bundles;

/// <summary> Serializes canonical v3 source and generated bundle descriptors. </summary>
public sealed class AgentDistributionBundleJsonSerializer
{
    private static readonly JsonWriterOptions WriterOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, Indented = true };

    /// <summary> Serializes the authored v3 definition. </summary>
    public string SerializeDefinition (AgentDistributionBundleDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return Serialize(writer => WriteShared(writer, definition.SchemaVersion, definition.CatalogId, definition.BundleVersion));
    }

    /// <summary> Serializes the generated v3 descriptor. </summary>
    public string SerializeDescriptor (AgentDistributionBundleDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return Serialize(writer =>
        {
            WriteShared(writer, descriptor.SchemaVersion, descriptor.CatalogId, descriptor.BundleVersion);
            writer.WriteString("bundleDigest", descriptor.BundleDigest.ToString());
        });
    }

    /// <summary> Deserializes an authored v3 definition. </summary>
    public AgentDistributionBundleDefinition DeserializeDefinition (string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        return new AgentDistributionBundleDefinition(root.GetProperty("schemaVersion").GetInt32(), new SkillCatalogId(root.GetProperty("catalogId").GetString() ?? string.Empty), new AgentDistributionBundleVersion(root.GetProperty("bundleVersion").GetInt32()));
    }

    /// <summary> Deserializes a generated v3 descriptor. </summary>
    public AgentDistributionBundleDescriptor DeserializeDescriptor (string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        return new AgentDistributionBundleDescriptor(root.GetProperty("schemaVersion").GetInt32(), new SkillCatalogId(root.GetProperty("catalogId").GetString() ?? string.Empty), new AgentDistributionBundleVersion(root.GetProperty("bundleVersion").GetInt32()), Sha256Digest.Parse(root.GetProperty("bundleDigest").GetString() ?? string.Empty));
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

    private static void WriteShared (Utf8JsonWriter writer, int schemaVersion, SkillCatalogId catalogId, AgentDistributionBundleVersion bundleVersion)
    {
        writer.WriteNumber("schemaVersion", schemaVersion);
        writer.WriteString("catalogId", catalogId.Value);
        writer.WriteNumber("bundleVersion", bundleVersion.Value);
    }
}
