using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using MackySoft.AgentSkills.Digests;
using MackySoft.AgentSkills.Manifests;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Installation.Validation;

/// <summary> Serializes legacy manifest shapes that must remain readable for installed-package compatibility. </summary>
internal sealed class SkillInstalledManifestLegacyCompatibilitySerializer
{
    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Indented = true,
    };

    public string SerializeSchemaVersionOneWithoutDependencies (SkillManifest manifest)
    {
        return SerializeSchemaVersionOneWithoutDependencies(manifest, includeManifestDigest: true);
    }

    public string ComputeSchemaVersionOneWithoutDependenciesManifestDigest (SkillManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var json = SerializeSchemaVersionOneWithoutDependencies(manifest, includeManifestDigest: false);
        return Sha256LowerHex.Compute(Encoding.UTF8.GetBytes(json));
    }

    private static string SerializeSchemaVersionOneWithoutDependencies (
        SkillManifest manifest,
        bool includeManifestDigest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, WriterOptions))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", manifest.SchemaVersion);
            writer.WriteString("catalogId", manifest.CatalogId.Value);
            writer.WriteString("tier", manifest.Tier.Value);
            writer.WriteString("contentDigest", manifest.ContentDigest);
            if (includeManifestDigest)
            {
                writer.WriteString("manifestDigest", manifest.ManifestDigest);
            }

            writer.WriteString("skillName", manifest.SkillName.Value);
            writer.WriteString("displayName", manifest.DisplayName);
            writer.WriteString("description", manifest.Description);
            writer.WritePropertyName("hostArtifacts");
            writer.WriteStartArray();

            foreach (var artifact in manifest.HostArtifacts.OrderBy(static artifact => artifact.Host, StringComparer.Ordinal))
            {
                writer.WriteStartObject();
                writer.WriteString("host", artifact.Host);
                if (!string.IsNullOrWhiteSpace(artifact.Path))
                {
                    writer.WriteString("path", artifact.Path);
                }

                if (!string.IsNullOrWhiteSpace(artifact.Digest))
                {
                    writer.WriteString("digest", artifact.Digest);
                }

                writer.WriteString("materializedFrontmatterDigest", artifact.MaterializedFrontmatterDigest);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        var json = SkillTextNormalizer.NormalizeToLf(Encoding.UTF8.GetString(stream.ToArray()));
        return json.EndsWith('\n') ? json : json + "\n";
    }
}
