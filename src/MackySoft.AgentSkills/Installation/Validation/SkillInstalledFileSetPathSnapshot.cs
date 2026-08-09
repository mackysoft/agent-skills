using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Installation.Validation;

internal static class SkillInstalledFileSetPathSnapshot
{
    public static IReadOnlyList<PackageRelativePath> Create (
        IReadOnlyList<PackageRelativePath> paths,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(paths, parameterName);

        var snapshot = paths.ToArray();
        var uniquePaths = new HashSet<PackageRelativePath>(PackageRelativePath.PortableFileSystemComparer);
        foreach (var path in snapshot)
        {
            ArgumentNullException.ThrowIfNull(path, parameterName);

            if (!uniquePaths.Add(path))
            {
                throw new ArgumentException("Installed file-set paths must not contain duplicates.", parameterName);
            }
        }

        Array.Sort(snapshot, static (left, right) => StringComparer.Ordinal.Compare(left.Value, right.Value));
        return Array.AsReadOnly(snapshot);
    }
}
