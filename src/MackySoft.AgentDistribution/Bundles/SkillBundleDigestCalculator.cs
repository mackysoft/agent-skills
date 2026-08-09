using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using MackySoft.AgentDistribution.Digests;
using MackySoft.AgentDistribution.Manifests;
using MackySoft.AgentDistribution.Packaging.Canonical;
using MackySoft.AgentDistribution.Shared;

namespace MackySoft.AgentDistribution.Bundles;

/// <summary> Computes the version-independent digest of a complete canonical SKILL package set. </summary>
public sealed class SkillBundleDigestCalculator
{
    private static readonly PackageRelativePath ManifestPath = PackageRelativePath.Parse("agent-skill.json");

    private readonly SkillManifestJsonSerializer manifestSerializer;

    /// <summary> Initializes a calculator with the canonical manifest projection contract. </summary>
    /// <param name="manifestSerializer"> The serializer that excludes release-only manifest fields from bundle digest input. </param>
    public SkillBundleDigestCalculator (SkillManifestJsonSerializer manifestSerializer)
    {
        this.manifestSerializer = manifestSerializer ?? throw new ArgumentNullException(nameof(manifestSerializer));
    }

    /// <summary>
    /// Computes SHA-256 over every canonical package file while excluding <c>skillBundleVersion</c> and
    /// <c>manifestDigest</c> from each manifest projection.
    /// </summary>
    /// <param name="packages"> A non-empty package set with unique skill-relative file paths. </param>
    /// <returns> The canonical SHA-256 digest value. </returns>
    /// <exception cref="ArgumentException"> Thrown when the package set is empty or contains a duplicate full file path. </exception>
    public Sha256Digest ComputeDigest (IEnumerable<CanonicalSkillPackage> packages)
    {
        ArgumentNullException.ThrowIfNull(packages);

        var entries = CreateEntries(packages).OrderBy(static entry => entry.RelativePath.Value, StringComparer.Ordinal).ToArray();
        if (entries.Length == 0)
        {
            throw new ArgumentException("Canonical SKILL bundle must contain at least one package file.", nameof(packages));
        }

        var duplicatePath = entries
            .GroupBy(static entry => entry.RelativePath)
            .FirstOrDefault(static group => group.Count() > 1);
        if (duplicatePath is not null)
        {
            throw new ArgumentException($"Canonical SKILL bundle contains a duplicate file path: {duplicatePath.Key}", nameof(packages));
        }

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var entry in entries)
        {
            AppendLengthPrefixedUtf8(hash, entry.RelativePath.Value);
            AppendLengthPrefixedUtf8(hash, SkillTextNormalizer.NormalizeToLf(entry.Content));
        }

        return Sha256Digest.GetHashAndReset(hash);
    }

    private IEnumerable<SkillDigestInputFile> CreateEntries (IEnumerable<CanonicalSkillPackage> packages)
    {
        foreach (var package in packages)
        {
            ArgumentNullException.ThrowIfNull(package);
            foreach (var file in package.Files)
            {
                ArgumentNullException.ThrowIfNull(file);

                var content = file.RelativePath == ManifestPath
                    ? manifestSerializer.SerializeForBundleDigest(package.Manifest)
                    : file.Content;
                yield return new SkillDigestInputFile(
                    PackageRelativePath.Parse($"{package.Manifest.SkillName.Value}/{file.RelativePath.Value}"),
                    content);
            }
        }
    }

    private static void AppendLengthPrefixedUtf8 (
        IncrementalHash hash,
        string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        Span<byte> lengthBytes = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(lengthBytes, bytes.Length);
        hash.AppendData(lengthBytes);
        hash.AppendData(bytes);
    }
}
