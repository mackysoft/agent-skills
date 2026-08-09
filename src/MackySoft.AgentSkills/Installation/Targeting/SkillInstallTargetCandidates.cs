using MackySoft.FileSystem;

namespace MackySoft.AgentSkills.Installation.Targeting;

/// <summary> Represents preferred and compatible bundle target roots for one default host target. </summary>
internal sealed class SkillInstallTargetCandidates
{
    internal SkillInstallTargetCandidates (
        IReadOnlyList<SkillResolvedInstallTarget> targets,
        AbsolutePath? defaultHostRoot,
        bool includesCatalogDirectoryLayout)
    {
        ArgumentNullException.ThrowIfNull(targets);
        if (targets.Count == 0)
        {
            throw new ArgumentException("Install target candidates must not be empty.", nameof(targets));
        }

        var snapshot = targets.ToArray();
        if (snapshot.Any(static target => target is null))
        {
            throw new ArgumentException("Install target candidates must not contain null items.", nameof(targets));
        }

        var host = snapshot[0].Host;
        if (snapshot.Any(target => target.Host != host))
        {
            throw new ArgumentException("Install target candidates must use one host.", nameof(targets));
        }

        if (snapshot.Select(static target => target.TargetRoot).Distinct().Count() != snapshot.Length)
        {
            throw new ArgumentException("Install target candidate roots must be unique.", nameof(targets));
        }

        if (defaultHostRoot is null)
        {
            if (snapshot.Length != 1)
            {
                throw new ArgumentException("An explicit install target must have exactly one candidate.", nameof(targets));
            }

            if (includesCatalogDirectoryLayout)
            {
                throw new ArgumentException(
                    "An explicit install target must not declare a default catalog-directory layout.",
                    nameof(includesCatalogDirectoryLayout));
            }
        }
        else
        {
            ArgumentNullException.ThrowIfNull(defaultHostRoot);
        }

        Targets = Array.AsReadOnly(snapshot);
        DefaultHostRoot = defaultHostRoot;
        IncludesCatalogDirectoryLayout = includesCatalogDirectoryLayout;
    }

    internal SkillResolvedInstallTarget PreferredTarget => Targets[0];

    internal IReadOnlyList<SkillResolvedInstallTarget> Targets { get; }

    internal AbsolutePath? DefaultHostRoot { get; }

    internal bool IncludesCatalogDirectoryLayout { get; }
}
