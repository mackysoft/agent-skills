using MackySoft.AgentSkills.Names;
using MackySoft.AgentSkills.Packaging.Canonical;

namespace MackySoft.AgentSkills.Installation.Requests;

internal static class SkillPackageInputSnapshot
{
    internal static IReadOnlyList<CanonicalSkillPackage> Create (
        IReadOnlyList<CanonicalSkillPackage> packages,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(packages, parameterName);

        var snapshot = new List<CanonicalSkillPackage>(packages.Count);
        var skillNames = new HashSet<SkillName>();
        foreach (var package in packages)
        {
            if (package is null)
            {
                throw new ArgumentException("SKILL packages must not contain null items.", parameterName);
            }

            if (!skillNames.Add(package.Manifest.SkillName))
            {
                throw new ArgumentException(
                    $"SKILL packages must contain unique SKILL names: {package.Manifest.SkillName.Value}",
                    parameterName);
            }

            snapshot.Add(package);
        }

        return Array.AsReadOnly(snapshot.ToArray());
    }
}
