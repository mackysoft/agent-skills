using MackySoft.AgentSkills.Agents.Packaging;
using MackySoft.AgentSkills.Bundles;
using MackySoft.AgentSkills.Packaging.Canonical;

namespace MackySoft.AgentSkills.Distribution;

/// <summary> Represents an immutable selected custom-agent package set and its resolved SKILL dependencies. </summary>
public sealed class AgentPackageCatalog
{
    /// <summary> Initializes a validated selected custom-agent package catalog. </summary>
    /// <param name="bundleDescriptor"> The descriptor that owns selected agents and resolved skills. </param>
    /// <param name="selectedCategories"> The caller's selected agent categories. </param>
    /// <param name="selectedAgentNames"> The caller's exact selected agent names. </param>
    /// <param name="selectedAgents"> The selected root agent packages. </param>
    /// <param name="resolvedSkills"> The transitive SKILL dependency closure required by <paramref name="selectedAgents" />. </param>
    internal AgentPackageCatalog (
        AgentSkillsBundleDescriptor bundleDescriptor,
        IReadOnlyList<AgentCategory> selectedCategories,
        IReadOnlyList<AgentName> selectedAgentNames,
        IReadOnlyList<CanonicalAgentPackage> selectedAgents,
        IReadOnlyList<CanonicalSkillPackage> resolvedSkills)
    {
        BundleDescriptor = bundleDescriptor ?? throw new ArgumentNullException(nameof(bundleDescriptor));
        SelectedCategories = CopyRequiredItems(selectedCategories, nameof(selectedCategories));
        SelectedAgentNames = CopyRequiredItems(selectedAgentNames, nameof(selectedAgentNames));
        SelectedAgents = CreateAgentSnapshot(selectedAgents, bundleDescriptor, nameof(selectedAgents));
        ResolvedSkills = CreateSkillSnapshot(resolvedSkills, bundleDescriptor, nameof(resolvedSkills));
    }

    /// <summary> Gets the descriptor that owns selected agents and resolved skills. </summary>
    public AgentSkillsBundleDescriptor BundleDescriptor { get; }

    /// <summary> Gets the caller's selected agent categories. </summary>
    public IReadOnlyList<AgentCategory> SelectedCategories { get; }

    /// <summary> Gets the caller's exact selected agent names. An empty selection means no name filter. </summary>
    public IReadOnlyList<AgentName> SelectedAgentNames { get; }

    /// <summary> Gets selected root agent packages ordered by agent name using ordinal comparison. </summary>
    public IReadOnlyList<CanonicalAgentPackage> SelectedAgents { get; }

    /// <summary> Gets transitive SKILL dependency packages ordered by skill name using ordinal comparison. </summary>
    public IReadOnlyList<CanonicalSkillPackage> ResolvedSkills { get; }

    private static IReadOnlyList<T> CopyRequiredItems<T> (
        IReadOnlyList<T> items,
        string parameterName)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(items, parameterName);

        var snapshot = new List<T>(items.Count);
        foreach (var item in items)
        {
            if (item is null)
            {
                throw new ArgumentException("Catalog collections must not contain null items.", parameterName);
            }

            snapshot.Add(item);
        }

        return Array.AsReadOnly(snapshot.ToArray());
    }

    private static IReadOnlyList<CanonicalAgentPackage> CreateAgentSnapshot (
        IReadOnlyList<CanonicalAgentPackage> agents,
        AgentSkillsBundleDescriptor bundleDescriptor,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(agents, parameterName);

        var snapshot = new List<CanonicalAgentPackage>(agents.Count);
        var agentNames = new HashSet<AgentName>();
        foreach (var agent in agents)
        {
            if (agent is null)
            {
                throw new ArgumentException("Catalog agents must not contain null items.", parameterName);
            }

            if (agent.Manifest.CatalogId != bundleDescriptor.CatalogId
                || agent.Manifest.BundleVersion != bundleDescriptor.BundleVersion)
            {
                throw new ArgumentException(
                    $"Catalog agent does not belong to the bundle descriptor: {agent.Manifest.AgentName.Value}",
                    parameterName);
            }

            if (!agentNames.Add(agent.Manifest.AgentName))
            {
                throw new ArgumentException(
                    $"Catalog agents must contain unique names: {agent.Manifest.AgentName.Value}",
                    parameterName);
            }

            snapshot.Add(agent);
        }

        return Array.AsReadOnly(snapshot
            .OrderBy(static agent => agent.Manifest.AgentName.Value, StringComparer.Ordinal)
            .ToArray());
    }

    private static IReadOnlyList<CanonicalSkillPackage> CreateSkillSnapshot (
        IReadOnlyList<CanonicalSkillPackage> skills,
        AgentSkillsBundleDescriptor bundleDescriptor,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(skills, parameterName);

        var snapshot = new List<CanonicalSkillPackage>(skills.Count);
        var skillNames = new HashSet<SkillName>();
        foreach (var skill in skills)
        {
            if (skill is null)
            {
                throw new ArgumentException("Catalog resolved skills must not contain null items.", parameterName);
            }

            if (skill.Manifest.CatalogId != bundleDescriptor.CatalogId
                || skill.Manifest.SkillBundleVersion.Value != bundleDescriptor.BundleVersion.Value)
            {
                throw new ArgumentException(
                    $"Catalog resolved skill does not belong to the bundle descriptor: {skill.Manifest.SkillName.Value}",
                    parameterName);
            }

            if (!skillNames.Add(skill.Manifest.SkillName))
            {
                throw new ArgumentException(
                    $"Catalog resolved skills must contain unique names: {skill.Manifest.SkillName.Value}",
                    parameterName);
            }

            snapshot.Add(skill);
        }

        return Array.AsReadOnly(snapshot
            .OrderBy(static skill => skill.Manifest.SkillName.Value, StringComparer.Ordinal)
            .ToArray());
    }
}
