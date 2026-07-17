using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Installation.Validation;

internal static class SkillInstalledFileSetPathSnapshot
{
    public static IReadOnlyList<string> Create (
        IReadOnlyList<string> paths,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(paths, parameterName);

        var snapshot = paths.ToArray();
        var uniquePaths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in snapshot)
        {
            if (!SkillRelativePath.IsSafeFilePath(path)
                || path.Contains(':', StringComparison.Ordinal)
                || path.Any(char.IsControl))
            {
                throw new ArgumentException(
                    "Installed file-set paths must be safe slash-separated paths relative to the SKILL directory.",
                    parameterName);
            }

            if (!uniquePaths.Add(path))
            {
                throw new ArgumentException("Installed file-set paths must not contain duplicates.", parameterName);
            }
        }

        Array.Sort(snapshot, StringComparer.Ordinal);
        return Array.AsReadOnly(snapshot);
    }
}
