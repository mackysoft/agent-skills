using System.Text;
using MackySoft.AgentSkills.Shared;
using MackySoft.FileSystem;

namespace MackySoft.AgentSkills.Bundles;

internal static class SkillBundleFileReader
{
    internal static async ValueTask<SkillOperationResult<string>> ReadUtf8WithoutByteOrderMarkAsync (
        AbsolutePath path,
        SkillFailureCode failureCode,
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
            return SkillOperationResult<string>.FailureResult(
                failureCode,
                $"bundle.json must be UTF-8 without a byte order mark: {path.Value}");
        }

        return SkillOperationResult<string>.Success(Encoding.UTF8.GetString(bytes));
    }
}
