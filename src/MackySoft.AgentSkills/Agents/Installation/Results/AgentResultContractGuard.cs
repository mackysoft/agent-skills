using MackySoft.FileSystem;

namespace MackySoft.AgentSkills.Agents.Installation.Results;

/// <summary> Validates path values retained by custom-agent operation results. </summary>
internal static class AgentResultContractGuard
{
    /// <summary> Validates and normalizes one required absolute result path. </summary>
    internal static string NormalizeAbsolutePath (
        string path,
        string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path, parameterName);
        if (!AbsolutePath.TryParse(path, out var absolutePath, out var failure))
        {
            throw new ArgumentException($"Agent operation result paths must be absolute: {failure.Message}", parameterName);
        }

        return absolutePath.Value;
    }
}
