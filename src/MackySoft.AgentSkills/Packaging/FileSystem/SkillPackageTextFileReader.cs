using System.Text;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Packaging.FileSystem;

/// <summary> Reads canonical package text as strict UTF-8 without a byte order mark. </summary>
internal static class SkillPackageTextFileReader
{
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    /// <summary> Reads one canonical package file without normalizing or replacing bytes. </summary>
    /// <param name="path"> The package file path. </param>
    /// <param name="cancellationToken"> The cancellation token propagated through file access. </param>
    /// <returns> The decoded text, or a manifest-invalid encoding failure. </returns>
    internal static async ValueTask<SkillOperationResult<string>> ReadAsync (
        string path,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        cancellationToken.ThrowIfCancellationRequested();

        var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        if (bytes.Length >= 3
            && bytes[0] == 0xEF
            && bytes[1] == 0xBB
            && bytes[2] == 0xBF)
        {
            return Failure($"SKILL package file must be UTF-8 without a byte order mark: {path}");
        }

        try
        {
            var content = StrictUtf8.GetString(bytes);
            if (!string.Equals(content, SkillTextNormalizer.NormalizeToLf(content), StringComparison.Ordinal))
            {
                return Failure($"SKILL package file must use LF line endings: {path}");
            }

            return SkillOperationResult<string>.Success(content);
        }
        catch (DecoderFallbackException)
        {
            return Failure($"SKILL package file must contain valid UTF-8: {path}");
        }
    }

    private static SkillOperationResult<string> Failure (string message)
    {
        return SkillOperationResult<string>.FailureResult(SkillFailureCodes.ManifestInvalid, message);
    }
}
