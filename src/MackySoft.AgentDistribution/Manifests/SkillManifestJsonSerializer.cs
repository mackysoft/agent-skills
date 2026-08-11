using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using MackySoft.AgentDistribution.Bundles;
using MackySoft.AgentDistribution.Catalogs;
using MackySoft.AgentDistribution.Digests;
using MackySoft.AgentDistribution.Shared;

namespace MackySoft.AgentDistribution.Manifests;

/// <summary> Serializes and reads canonical <c>agent-skill.json</c> manifests. </summary>
public sealed class SkillManifestJsonSerializer
{
    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Indented = true,
    };

    /// <summary> Serializes one manifest to deterministic JSON. </summary>
    /// <param name="manifest"> The manifest. </param>
    /// <returns> The serialized JSON with LF line endings and a trailing newline. </returns>
    public string Serialize (SkillManifest manifest)
    {
        return Serialize(manifest, includeSkillBundleVersion: true, includeManifestDigest: true);
    }

    internal string SerializeWithoutManifestDigest (SkillManifest manifest)
    {
        return Serialize(manifest, includeSkillBundleVersion: true, includeManifestDigest: false);
    }

    /// <summary> Serializes the version-independent manifest projection used by bundle digest calculation. </summary>
    /// <param name="manifest"> The manifest to project. </param>
    /// <returns> Canonical JSON without <c>skillBundleVersion</c> or <c>manifestDigest</c>. </returns>
    internal string SerializeForBundleDigest (SkillManifest manifest)
    {
        return Serialize(manifest, includeSkillBundleVersion: false, includeManifestDigest: false);
    }

    private static string Serialize (
        SkillManifest manifest,
        bool includeSkillBundleVersion,
        bool includeManifestDigest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, WriterOptions))
        {
            WriteManifest(writer, manifest, includeSkillBundleVersion, includeManifestDigest);
        }

        var json = AgentDistributionTextNormalizer.NormalizeToLf(Encoding.UTF8.GetString(stream.ToArray()));
        return json.EndsWith('\n') ? json : json + "\n";
    }

    /// <summary> Reads one untrusted manifest candidate from JSON text. </summary>
    /// <param name="json"> The JSON text. </param>
    /// <returns> The parsed manifest candidate. </returns>
    /// <exception cref="JsonException"> Thrown when the JSON is invalid. </exception>
    internal SkillManifestCandidate Deserialize (string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var artifacts = root.GetProperty("hostArtifacts")
            .EnumerateArray()
            .Select(static element => new SkillHostArtifactManifest(
                ReadHost(element.GetProperty("host")),
                element.TryGetProperty("path", out var pathElement)
                    ? PackageRelativePath.Parse(pathElement.GetString() ?? string.Empty)
                    : null,
                element.TryGetProperty("digest", out var digestElement)
                    ? Sha256Digest.Parse(digestElement.GetString() ?? string.Empty)
                    : null,
                Sha256Digest.Parse(element.GetProperty("materializedFrontmatterDigest").GetString() ?? string.Empty)))
            .ToArray();

        return new SkillManifestCandidate(
            root.GetProperty("schemaVersion").GetInt32(),
            new SkillBundleVersion(root.GetProperty("skillBundleVersion").GetInt32()),
            new AgentDistributionCatalogId(root.GetProperty("catalogId").GetString() ?? string.Empty),
            new SkillCategory(root.GetProperty("category").GetString() ?? string.Empty),
            new SkillName(root.GetProperty("skillName").GetString() ?? string.Empty),
            root.GetProperty("displayName").GetString() ?? string.Empty,
            root.GetProperty("description").GetString() ?? string.Empty,
            ReadDependencies(root),
            Sha256Digest.Parse(root.GetProperty("contentDigest").GetString() ?? string.Empty),
            Sha256Digest.Parse(root.GetProperty("manifestDigest").GetString() ?? string.Empty),
            artifacts);
    }

    /// <summary> Reads one manifest from JSON text without leaking parse exceptions. </summary>
    /// <param name="json"> The JSON text. </param>
    /// <returns> The parsed manifest candidate or manifest-invalid failure. </returns>
    internal AgentDistributionOperationResult<SkillManifestCandidate> TryDeserialize (string json)
    {
        try
        {
            return AgentDistributionOperationResult<SkillManifestCandidate>.Success(Deserialize(json));
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or ArgumentException or KeyNotFoundException or FormatException)
        {
            return AgentDistributionOperationResult<SkillManifestCandidate>.FailureResult(
                AgentDistributionFailureCodes.ManifestInvalid,
                "agent-skill.json is invalid.");
        }
    }

    private static void WriteManifest (
        Utf8JsonWriter writer,
        SkillManifest manifest,
        bool includeSkillBundleVersion,
        bool includeManifestDigest)
    {
        WriteManifest(
            writer,
            manifest.SchemaVersion,
            manifest.SkillBundleVersion,
            manifest.CatalogId,
            manifest.Category,
            manifest.SkillName,
            manifest.DisplayName,
            manifest.Description,
            manifest.Dependencies,
            manifest.ContentDigest,
            manifest.ManifestDigest,
            manifest.HostArtifacts,
            includeSkillBundleVersion,
            includeManifestDigest);
    }

    internal string SerializeWithoutManifestDigest (SkillManifestCandidate manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, WriterOptions))
        {
            WriteManifest(
                writer,
                manifest.SchemaVersion,
                manifest.SkillBundleVersion,
                manifest.CatalogId,
                manifest.Category,
                manifest.SkillName,
                manifest.DisplayName,
                manifest.Description,
                manifest.Dependencies,
                manifest.ContentDigest,
                null,
                manifest.HostArtifacts,
                includeSkillBundleVersion: true,
                includeManifestDigest: false);
        }

        var json = AgentDistributionTextNormalizer.NormalizeToLf(Encoding.UTF8.GetString(stream.ToArray()));
        return json.EndsWith('\n') ? json : json + "\n";
    }

    private static void WriteManifest (
        Utf8JsonWriter writer,
        int schemaVersion,
        SkillBundleVersion skillBundleVersion,
        AgentDistributionCatalogId catalogId,
        SkillCategory category,
        SkillName skillName,
        string displayName,
        string description,
        IReadOnlyList<SkillName> dependencies,
        Sha256Digest contentDigest,
        Sha256Digest? manifestDigest,
        IReadOnlyList<SkillHostArtifactManifest> hostArtifacts,
        bool includeSkillBundleVersion,
        bool includeManifestDigest)
    {
        writer.WriteStartObject();
        writer.WriteNumber("schemaVersion", schemaVersion);
        if (includeSkillBundleVersion)
        {
            writer.WriteNumber("skillBundleVersion", skillBundleVersion.Value);
        }

        writer.WriteString("catalogId", catalogId.Value);
        writer.WriteString("category", category.Value);
        writer.WriteString("skillName", skillName.Value);
        writer.WriteString("displayName", displayName);
        writer.WriteString("description", description);
        writer.WritePropertyName("dependencies");
        writer.WriteStartArray();
        foreach (var dependency in dependencies.OrderBy(static dependency => dependency.Value, StringComparer.Ordinal))
        {
            writer.WriteStringValue(dependency.Value);
        }

        writer.WriteEndArray();
        writer.WriteString("contentDigest", contentDigest.ToString());
        if (includeManifestDigest)
        {
            writer.WriteString("manifestDigest", manifestDigest!.ToString());
        }

        writer.WritePropertyName("hostArtifacts");
        WriteHostArtifacts(writer, hostArtifacts);
        writer.WriteEndObject();
    }

    private static void WriteHostArtifacts (
        Utf8JsonWriter writer,
        IReadOnlyList<SkillHostArtifactManifest> hostArtifacts)
    {
        writer.WriteStartArray();

        foreach (var artifact in hostArtifacts.OrderBy(static artifact => artifact.Host))
        {
            writer.WriteStartObject();
            writer.WriteString("host", Vocabulary.GetText(artifact.Host));
            if (artifact.Path is not null)
            {
                writer.WriteString("path", artifact.Path.Value);
            }

            if (artifact.Digest is not null)
            {
                writer.WriteString("digest", artifact.Digest.ToString());
            }

            writer.WriteString("materializedFrontmatterDigest", artifact.MaterializedFrontmatterDigest.ToString());
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static HostKind ReadHost (JsonElement element)
    {
        var literal = element.GetString();
        if (!Vocabulary.TryGetValue(literal, out HostKind host))
        {
            throw new JsonException($"Unsupported SKILL host literal: {literal ?? "(null)"}.");
        }

        return host;
    }

    private static IReadOnlyList<SkillName> ReadDependencies (JsonElement root)
    {
        return root.GetProperty("dependencies")
            .EnumerateArray()
            .Select(static element => new SkillName(element.GetString() ?? string.Empty))
            .ToArray();
    }
}
