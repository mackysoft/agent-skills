using System.Text;
using MackySoft.AgentDistribution.Shared;
using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Packaging.Canonical;

/// <summary>Reads canonical package text as strict UTF-8 without a byte order mark.</summary>
internal static class CanonicalPackageTextReader
{
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    /// <summary>Reads one canonical package file without normalizing or replacing bytes.</summary>
    internal static async ValueTask<AgentDistributionOperationResult<string>> ReadAsync (
        AbsolutePath path,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(path);
        cancellationToken.ThrowIfCancellationRequested();

        var bytes = await File.ReadAllBytesAsync(path.Value, cancellationToken).ConfigureAwait(false);
        if (bytes.Length >= 3
            && bytes[0] == 0xEF
            && bytes[1] == 0xBB
            && bytes[2] == 0xBF)
        {
            return Failure($"Canonical package file must be UTF-8 without a byte order mark: {path}");
        }

        try
        {
            var content = StrictUtf8.GetString(bytes);
            if (!string.Equals(content, AgentDistributionTextNormalizer.NormalizeToLf(content), StringComparison.Ordinal))
            {
                return Failure($"Canonical package file must use LF line endings: {path}");
            }

            return AgentDistributionOperationResult<string>.Success(content);
        }
        catch (DecoderFallbackException)
        {
            return Failure($"Canonical package file must contain valid UTF-8: {path}");
        }
    }

    private static AgentDistributionOperationResult<string> Failure (string message)
    {
        return AgentDistributionOperationResult<string>.FailureResult(AgentDistributionFailureCodes.ManifestInvalid, message);
    }
}
