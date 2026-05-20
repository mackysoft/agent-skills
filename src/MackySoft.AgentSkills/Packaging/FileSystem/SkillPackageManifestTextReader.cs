using System.Text;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Packaging.FileSystem;

internal static class SkillPackageManifestTextReader
{
    public static async ValueTask<SkillOperationResult<string>> ReadUtf8WithoutByteOrderMarkAsync (
        string manifestPath,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);
        cancellationToken.ThrowIfCancellationRequested();

        var bytes = await File.ReadAllBytesAsync(manifestPath, cancellationToken).ConfigureAwait(false);
        if (HasUtf8ByteOrderMark(bytes))
        {
            return SkillOperationResult<string>.FailureResult(
                SkillFailureCodes.ManifestInvalid,
                $"agent-skill.json must be UTF-8 without a byte order mark: {manifestPath}");
        }

        return SkillOperationResult<string>.Success(Encoding.UTF8.GetString(bytes));
    }

    private static bool HasUtf8ByteOrderMark (ReadOnlySpan<byte> bytes)
    {
        return bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
    }
}
