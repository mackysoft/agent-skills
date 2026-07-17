using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using MackySoft.AgentSkills.Catalogs;
using MackySoft.AgentSkills.Digests;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Bundles;

/// <summary> Serializes authored and generated <c>bundle.json</c> documents in their canonical forms. </summary>
public sealed class SkillBundleJsonSerializer
{
    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Indented = true,
    };

    /// <summary> Serializes the authored source form without a generated digest. </summary>
    /// <param name="definition"> The validated authored bundle definition. </param>
    /// <returns> Canonical JSON with LF line endings and a trailing newline. </returns>
    public string SerializeDefinition (SkillBundleDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        return Serialize(writer =>
        {
            WriteSharedProperties(writer, definition.SchemaVersion, definition.CatalogId, definition.SkillBundleVersion);
        });
    }

    /// <summary> Serializes the generated runtime form including its package-set digest. </summary>
    /// <param name="descriptor"> The validated generated bundle descriptor. </param>
    /// <returns> Canonical JSON with LF line endings and a trailing newline. </returns>
    public string SerializeDescriptor (SkillBundleDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        return Serialize(writer =>
        {
            WriteSharedProperties(writer, descriptor.SchemaVersion, descriptor.CatalogId, descriptor.SkillBundleVersion);
            writer.WriteString("bundleDigest", descriptor.BundleDigest.ToString());
        });
    }

    /// <summary> Reads the authored source form. Shape and canonical-text validation remain the reader's responsibility. </summary>
    /// <param name="json"> The source JSON text. </param>
    /// <returns> The parsed authored bundle definition. </returns>
    /// <exception cref="JsonException"> Thrown when required JSON values cannot be read. </exception>
    /// <exception cref="ArgumentException"> Thrown when a required value is missing or invalid. </exception>
    public SkillBundleDefinition DeserializeDefinition (string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        return new SkillBundleDefinition(
            root.GetProperty("schemaVersion").GetInt32(),
            new SkillCatalogId(root.GetProperty("catalogId").GetString() ?? string.Empty),
            root.GetProperty("skillBundleVersion").GetInt32());
    }

    /// <summary> Reads the generated runtime form. Shape and canonical-text validation remain the reader's responsibility. </summary>
    /// <param name="json"> The generated descriptor JSON text. </param>
    /// <returns> The parsed generated bundle descriptor. </returns>
    /// <exception cref="JsonException"> Thrown when required JSON values cannot be read. </exception>
    /// <exception cref="ArgumentException"> Thrown when a required value is missing or invalid. </exception>
    /// <exception cref="FormatException"> Thrown when <c>bundleDigest</c> is not canonical SHA-256 text. </exception>
    public SkillBundleDescriptor DeserializeDescriptor (string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        return new SkillBundleDescriptor(
            root.GetProperty("schemaVersion").GetInt32(),
            new SkillCatalogId(root.GetProperty("catalogId").GetString() ?? string.Empty),
            root.GetProperty("skillBundleVersion").GetInt32(),
            Sha256Digest.Parse(root.GetProperty("bundleDigest").GetString() ?? string.Empty));
    }

    private static string Serialize (Action<Utf8JsonWriter> writeProperties)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, WriterOptions))
        {
            writer.WriteStartObject();
            writeProperties(writer);
            writer.WriteEndObject();
        }

        var json = SkillTextNormalizer.NormalizeToLf(Encoding.UTF8.GetString(stream.ToArray()));
        return json.EndsWith('\n') ? json : json + "\n";
    }

    private static void WriteSharedProperties (
        Utf8JsonWriter writer,
        int schemaVersion,
        SkillCatalogId catalogId,
        int skillBundleVersion)
    {
        writer.WriteNumber("schemaVersion", schemaVersion);
        writer.WriteString("catalogId", catalogId.Value);
        writer.WriteNumber("skillBundleVersion", skillBundleVersion);
    }
}
