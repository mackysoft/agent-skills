using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using MackySoft.AgentDistribution.Bundles;
using MackySoft.AgentDistribution.Catalogs;
using MackySoft.AgentDistribution.Digests;
using MackySoft.AgentDistribution.Shared;

namespace MackySoft.AgentDistribution.Agents.Manifests;

/// <summary> Serializes canonical generated custom-agent manifests. </summary>
public sealed class AgentManifestJsonSerializer
{
    private static readonly JsonWriterOptions WriterOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, Indented = true };

    /// <summary> Serializes the complete manifest. </summary>
    public string Serialize (AgentManifest manifest) => Serialize(manifest, includeBundleVersion: true, includeManifestDigest: true);

    /// <summary> Serializes the version-independent projection for bundle digest calculation. </summary>
    internal string SerializeForBundleDigest (AgentManifest manifest) => Serialize(manifest, includeBundleVersion: false, includeManifestDigest: false);

    /// <summary> Serializes the manifest-digest projection. </summary>
    internal string SerializeWithoutManifestDigest (AgentManifest manifest) => Serialize(manifest, includeBundleVersion: true, includeManifestDigest: false);

    /// <summary> Reads an untrusted manifest. </summary>
    internal AgentManifest Deserialize (string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var artifacts = root.GetProperty("hostArtifacts").EnumerateArray().Select(static item => new AgentHostArtifactManifest(ParseHost(item.GetProperty("host").GetString() ?? string.Empty), PackageRelativePath.Parse(item.GetProperty("path").GetString() ?? string.Empty), Sha256Digest.Parse(item.GetProperty("digest").GetString() ?? string.Empty))).ToArray();
        return new AgentManifest(root.GetProperty("schemaVersion").GetInt32(), new AgentDistributionBundleVersion(root.GetProperty("bundleVersion").GetInt32()), new AgentDistributionCatalogId(root.GetProperty("catalogId").GetString() ?? string.Empty), new AgentName(root.GetProperty("agentName").GetString() ?? string.Empty), root.GetProperty("displayName").GetString() ?? string.Empty, root.GetProperty("description").GetString() ?? string.Empty, root.GetProperty("skillDependencies").EnumerateArray().Select(static item => new SkillName(item.GetString() ?? string.Empty)).ToArray(), Sha256Digest.Parse(root.GetProperty("contentDigest").GetString() ?? string.Empty), Sha256Digest.Parse(root.GetProperty("manifestDigest").GetString() ?? string.Empty), artifacts);
    }

    private static string Serialize (AgentManifest manifest, bool includeBundleVersion, bool includeManifestDigest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, WriterOptions))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", manifest.SchemaVersion);
            if (includeBundleVersion)
            {
                writer.WriteNumber("bundleVersion", manifest.BundleVersion.Value);
            }

            writer.WriteString("catalogId", manifest.CatalogId.Value);
            writer.WriteString("agentName", manifest.AgentName.Value);
            writer.WriteString("displayName", manifest.DisplayName);
            writer.WriteString("description", manifest.Description);
            writer.WritePropertyName("skillDependencies");
            writer.WriteStartArray();
            foreach (var dependency in manifest.SkillDependencies)
            {
                writer.WriteStringValue(dependency.Value);
            }

            writer.WriteEndArray();
            writer.WriteString("contentDigest", manifest.ContentDigest.ToString());
            if (includeManifestDigest)
            {
                writer.WriteString("manifestDigest", manifest.ManifestDigest.ToString());
            }

            writer.WritePropertyName("hostArtifacts");
            writer.WriteStartArray();
            foreach (var artifact in manifest.HostArtifacts)
            {
                writer.WriteStartObject();
                writer.WriteString("host", Vocabulary.GetText(artifact.HostId));
                writer.WriteString("path", artifact.Path.Value);
                writer.WriteString("digest", artifact.Digest.ToString());
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        var result = AgentDistributionTextNormalizer.NormalizeToLf(Encoding.UTF8.GetString(stream.ToArray()));
        return result.EndsWith('\n') ? result : result + "\n";
    }

    private static HostKind ParseHost (string literal)
    {
        if (!Vocabulary.TryGetValue(literal, out HostKind host))
        {
            throw new ArgumentException("Agent manifest host is unsupported.", nameof(literal));
        }

        return host;
    }
}
