using MackySoft.AgentSkills.Agents.Manifests;
using MackySoft.AgentSkills.Digests;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Agents.Packaging;

/// <summary> Represents a validated canonical custom-agent package. </summary>
public sealed class CanonicalAgentPackage
{
    /// <summary> Initializes a canonical package after validating its manifest and files. </summary>
    internal CanonicalAgentPackage (AgentManifest manifest, IReadOnlyList<SkillPackageFile> files, AgentManifestJsonSerializer serializer, SkillDigestCalculator digestCalculator)
    {
        Manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(serializer);
        ArgumentNullException.ThrowIfNull(digestCalculator);
        var snapshot = files.OrderBy(static file => file.RelativePath, StringComparer.Ordinal).ToArray();
        if (snapshot.Any(static file => file is null) || snapshot.GroupBy(static file => file.RelativePath, StringComparer.Ordinal).Any(static group => group.Count() != 1))
        {
            throw new ArgumentException("Agent package files must be unique and non-null.", nameof(files));
        }

        var byPath = snapshot.ToDictionary(static file => file.RelativePath, StringComparer.Ordinal);
        if (!byPath.TryGetValue("AGENT.md", out var instructions) || !byPath.TryGetValue("agent-manifest.json", out var manifestFile) || !string.Equals(manifestFile.Content, serializer.Serialize(manifest), StringComparison.Ordinal))
        {
            throw new ArgumentException("Agent package files do not match the canonical manifest.", nameof(files));
        }

        if (digestCalculator.ComputeSingleFileDigest("AGENT.md", instructions.Content) != manifest.ContentDigest)
        {
            throw new ArgumentException("Agent content digest does not match AGENT.md.", nameof(files));
        }

        var expectedPaths = manifest.HostArtifacts.Select(static artifact => artifact.Path).Append("AGENT.md").Append("agent-manifest.json").ToHashSet(StringComparer.Ordinal);
        if (!expectedPaths.SetEquals(byPath.Keys))
        {
            throw new ArgumentException("Agent package file set does not match manifest artifacts.", nameof(files));
        }

        foreach (var artifact in manifest.HostArtifacts)
        {
            if (!byPath.TryGetValue(artifact.Path, out var file) || digestCalculator.ComputeSingleFileDigest(artifact.Path, file.Content) != artifact.Digest)
            {
                throw new ArgumentException("Agent host artifact digest does not match files.", nameof(files));
            }
        }

        Files = Array.AsReadOnly(snapshot);
    }

    /// <summary> Gets the manifest. </summary>
    public AgentManifest Manifest { get; }

    /// <summary> Gets canonical package files. </summary>
    public IReadOnlyList<SkillPackageFile> Files { get; }
}
