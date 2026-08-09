using MackySoft.AgentDistribution.Agents.Manifests;
using MackySoft.AgentDistribution.Digests;
using MackySoft.AgentDistribution.Shared;

namespace MackySoft.AgentDistribution.Agents.Packaging;

/// <summary> Represents a validated canonical custom-agent package. </summary>
public sealed class CanonicalAgentPackage
{
    /// <summary> Initializes a canonical package after validating its manifest and files. </summary>
    internal CanonicalAgentPackage (AgentManifest manifest, IReadOnlyList<PackageTextFile> files, AgentManifestJsonSerializer serializer, SkillDigestCalculator digestCalculator)
    {
        Manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(serializer);
        ArgumentNullException.ThrowIfNull(digestCalculator);
        var snapshot = files.OrderBy(static file => file.RelativePath.Value, StringComparer.Ordinal).ToArray();
        if (snapshot.Any(static file => file is null)
            || snapshot.GroupBy(static file => file.RelativePath, PackageRelativePath.PortableFileSystemComparer).Any(static group => group.Count() != 1))
        {
            throw new ArgumentException("Agent package files must be unique and non-null.", nameof(files));
        }

        var byPath = snapshot.ToDictionary(static file => file.RelativePath);
        var instructionsPath = PackageRelativePath.Parse("AGENT.md");
        var manifestPath = PackageRelativePath.Parse("agent-manifest.json");
        if (!byPath.TryGetValue(instructionsPath, out var instructions) || !byPath.TryGetValue(manifestPath, out var manifestFile) || !string.Equals(manifestFile.Content, serializer.Serialize(manifest), StringComparison.Ordinal))
        {
            throw new ArgumentException("Agent package files do not match the canonical manifest.", nameof(files));
        }

        if (digestCalculator.ComputeSingleFileDigest(instructionsPath, instructions.Content) != manifest.ContentDigest)
        {
            throw new ArgumentException("Agent content digest does not match AGENT.md.", nameof(files));
        }

        var expectedPaths = manifest.HostArtifacts.Select(static artifact => artifact.Path).Append(instructionsPath).Append(manifestPath).ToHashSet();
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
    public IReadOnlyList<PackageTextFile> Files { get; }
}
