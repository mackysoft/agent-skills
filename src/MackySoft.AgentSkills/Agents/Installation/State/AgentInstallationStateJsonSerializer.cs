using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using MackySoft.AgentSkills.Bundles;
using MackySoft.AgentSkills.Catalogs;
using MackySoft.AgentSkills.Digests;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Agents.Installation.State;

/// <summary> Serializes canonical agent installation ownership state. </summary>
public sealed class AgentInstallationStateJsonSerializer
{
    private static readonly string[] ExpectedProperties = ["schemaVersion", "bundleVersion", "catalogId", "hostId", "category", "agentName", "agentManifestDigest", "managedArtifacts"];
    private static readonly string[] ExpectedArtifactProperties = ["path", "digest"];
    private static readonly JsonWriterOptions WriterOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, Indented = true };

    /// <summary> Serializes one installation state in canonical JSON form. </summary>
    public string Serialize (AgentInstallationState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, WriterOptions))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", state.SchemaVersion);
            writer.WriteNumber("bundleVersion", state.BundleVersion.Value);
            writer.WriteString("catalogId", state.CatalogId.Value);
            writer.WriteString("hostId", Vocabulary.GetText(state.HostId));
            writer.WriteString("category", state.Category.Value);
            writer.WriteString("agentName", state.AgentName.Value);
            writer.WriteString("agentManifestDigest", state.AgentManifestDigest.ToString());
            writer.WritePropertyName("managedArtifacts");
            writer.WriteStartArray();
            foreach (var artifact in state.ManagedArtifacts)
            {
                writer.WriteStartObject();
                writer.WriteString("path", artifact.Path.Value);
                writer.WriteString("digest", artifact.Digest.ToString());
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return SkillTextNormalizer.NormalizeToLf(Encoding.UTF8.GetString(stream.ToArray())) + "\n";
    }

    /// <summary> Reads canonical installation state or returns a manifest-invalid failure. </summary>
    public SkillOperationResult<AgentInstallationState> TryDeserialize (string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !ExpectedProperties.SequenceEqual(root.EnumerateObject().Select(static property => property.Name), StringComparer.Ordinal))
            {
                return Failure("Agent installation state has an invalid property shape.");
            }

            var artifacts = root.GetProperty("managedArtifacts").EnumerateArray().Select(static artifact =>
            {
                if (artifact.ValueKind != JsonValueKind.Object
                    || !ExpectedArtifactProperties.SequenceEqual(artifact.EnumerateObject().Select(static property => property.Name), StringComparer.Ordinal))
                {
                    throw new FormatException("Agent installation state managed artifact has an invalid property shape.");
                }

                var pathText = artifact.GetProperty("path").GetString();
                if (!PackageRelativePath.TryParse(pathText, out var path))
                {
                    throw new FormatException("Agent installation state managed artifact path is invalid.");
                }

                return new AgentInstalledArtifact(
                    path,
                    Sha256Digest.Parse(artifact.GetProperty("digest").GetString() ?? string.Empty));
            }).ToArray();
            var state = new AgentInstallationState(
                root.GetProperty("schemaVersion").GetInt32(),
                new AgentSkillsBundleVersion(root.GetProperty("bundleVersion").GetInt32()),
                new SkillCatalogId(root.GetProperty("catalogId").GetString() ?? string.Empty),
                ParseHost(root.GetProperty("hostId").GetString() ?? string.Empty),
                new AgentCategory(root.GetProperty("category").GetString() ?? string.Empty),
                new AgentName(root.GetProperty("agentName").GetString() ?? string.Empty),
                Sha256Digest.Parse(root.GetProperty("agentManifestDigest").GetString() ?? string.Empty),
                artifacts);
            return string.Equals(json, Serialize(state), StringComparison.Ordinal)
                ? SkillOperationResult<AgentInstallationState>.Success(state)
                : Failure("Agent installation state is not canonical JSON.");
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or FormatException or InvalidOperationException)
        {
            return Failure("Agent installation state is invalid.");
        }
    }

    private static SkillOperationResult<AgentInstallationState> Failure (string message)
    {
        return SkillOperationResult<AgentInstallationState>.FailureResult(SkillFailureCodes.ManifestInvalid, message);
    }

    private static HostKind ParseHost (string literal)
    {
        if (!Vocabulary.TryGetValue(literal, out HostKind host))
        {
            throw new ArgumentException("Agent installation state host is unsupported.", nameof(literal));
        }

        return host;
    }
}
