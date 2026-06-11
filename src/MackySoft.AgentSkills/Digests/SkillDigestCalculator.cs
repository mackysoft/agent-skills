using System.Security.Cryptography;
using System.Text;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Digests;

/// <summary> Computes deterministic SKILL content and host artifact digests. </summary>
public sealed class SkillDigestCalculator
{
    private static readonly byte[] Separator = [0];

    /// <summary> Computes one digest from normalized digest input files. </summary>
    /// <param name="files"> The files included in the digest input. </param>
    /// <returns> The lowercase hexadecimal SHA-256 digest. </returns>
    public string ComputeDigest (IEnumerable<SkillDigestInputFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var file in files.OrderBy(static file => file.RelativePath, StringComparer.Ordinal))
        {
            ValidateRelativePath(file.RelativePath);
            AppendUtf8(hash, file.RelativePath);
            hash.AppendData(Separator);
            AppendUtf8(hash, SkillTextNormalizer.NormalizeToLf(file.Content));
        }

        return Sha256LowerHex.GetHashAndReset(hash);
    }

    /// <summary> Computes one digest for a single text artifact. </summary>
    /// <param name="relativePath"> The artifact path. </param>
    /// <param name="content"> The artifact content. </param>
    /// <returns> The lowercase hexadecimal SHA-256 digest. </returns>
    public string ComputeSingleFileDigest (
        string relativePath,
        string content)
    {
        return ComputeDigest([new SkillDigestInputFile(relativePath, content)]);
    }

    private static void ValidateRelativePath (string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        if (relativePath.Contains('\\', StringComparison.Ordinal)
            || relativePath.StartsWith("/", StringComparison.Ordinal)
            || relativePath.Split('/').Any(static segment => string.IsNullOrWhiteSpace(segment) || segment is "." or ".."))
        {
            throw new ArgumentException("Digest file path must be a safe slash-separated relative path.", nameof(relativePath));
        }
    }

    private static void AppendUtf8 (
        IncrementalHash hash,
        string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        hash.AppendData(bytes);
    }

}
