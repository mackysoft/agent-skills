using System.Text.RegularExpressions;
using MackySoft.AgentSkills.Names;

namespace MackySoft.AgentSkills.Sources;

internal static partial class SkillDependencyReferenceScanner
{
    public static IReadOnlyList<SkillName> FindReferences (IEnumerable<string> texts)
    {
        ArgumentNullException.ThrowIfNull(texts);

        var references = new HashSet<SkillName>();
        foreach (var text in texts)
        {
            if (string.IsNullOrEmpty(text))
            {
                continue;
            }

            foreach (Match match in DependencyReferenceRegex().Matches(text))
            {
                if (SkillName.TryCreate(match.Groups["skillName"].Value, out var skillName))
                {
                    references.Add(skillName);
                }
            }
        }

        return references
            .OrderBy(static skillName => skillName.Value, StringComparer.Ordinal)
            .ToArray();
    }

    [GeneratedRegex(@"\$(?<skillName>[a-z0-9][a-z0-9-]*)(?![A-Za-z0-9_-])", RegexOptions.CultureInvariant)]
    private static partial Regex DependencyReferenceRegex ();
}
