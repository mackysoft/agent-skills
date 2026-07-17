using System.Security.Cryptography;
using System.Text;

namespace MackySoft.AgentSkills.Digests;

/// <summary> Computes deterministic SKILL content and host artifact digests. </summary>
public sealed class SkillDigestCalculator
{
    private static readonly byte[] Separator = [0];

    /// <summary> Computes one digest from normalized digest input files. </summary>
    /// <param name="files"> The files included in the digest input. </param>
    /// <returns> The lowercase hexadecimal SHA-256 digest. </returns>
    public Sha256Digest ComputeDigest (IEnumerable<SkillDigestInputFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var file in files.OrderBy(static file => file.RelativePath, StringComparer.Ordinal))
        {
            AppendUtf8(hash, file.RelativePath);
            hash.AppendData(Separator);
            AppendUtf8(hash, file.Content);
        }

        return Sha256Digest.GetHashAndReset(hash);
    }

    /// <summary> Computes one digest for a single text artifact. </summary>
    /// <param name="relativePath"> The artifact path. </param>
    /// <param name="content"> The artifact content. </param>
    /// <returns> The lowercase hexadecimal SHA-256 digest. </returns>
    public Sha256Digest ComputeSingleFileDigest (
        string relativePath,
        string content)
    {
        return ComputeDigest([new SkillDigestInputFile(relativePath, content)]);
    }

    private static void AppendUtf8 (
        IncrementalHash hash,
        string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        hash.AppendData(bytes);
    }

}
