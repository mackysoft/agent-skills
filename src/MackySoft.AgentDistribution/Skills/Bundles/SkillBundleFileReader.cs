using System.Text;
using MackySoft.AgentDistribution.Shared;
using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Skills.Bundles;

internal static class SkillBundleFileReader
{
    internal static async ValueTask<AgentDistributionOperationResult<string>> ReadUtf8WithoutByteOrderMarkAsync (
        AbsolutePath path,
        AgentDistributionFailureCode failureCode,
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
            return AgentDistributionOperationResult<string>.FailureResult(
                failureCode,
                $"bundle.json must be UTF-8 without a byte order mark: {path.Value}");
        }

        return AgentDistributionOperationResult<string>.Success(Encoding.UTF8.GetString(bytes));
    }
}
