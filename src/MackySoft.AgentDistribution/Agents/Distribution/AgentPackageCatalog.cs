using MackySoft.AgentDistribution.Agents.Manifests;
using MackySoft.AgentDistribution.Agents.Packaging;
using MackySoft.AgentDistribution.Catalogs;
using MackySoft.AgentDistribution.Skills.Packaging.Canonical;

namespace MackySoft.AgentDistribution.Agents.Distribution;

/// <summary> Represents an immutable selected custom-agent package set and its resolved SKILL dependencies. </summary>
public sealed class AgentPackageCatalog
{
    /// <summary> Initializes a validated selected custom-agent package catalog. </summary>
    /// <param name="catalogId"> The catalog that owns selected agents and resolved skills. </param>
    /// <param name="bundleVersion"> The generated bundle version shared by selected packages. </param>
    /// <param name="selectedAgentNames"> The caller's exact selected agent names. </param>
    /// <param name="selectedAgents"> The selected root agent packages. </param>
    /// <param name="resolvedSkills"> The transitive SKILL dependency closure required by <paramref name="selectedAgents" />. </param>
    internal AgentPackageCatalog (
        AgentDistributionCatalogId catalogId,
        AgentDistributionBundleVersion bundleVersion,
        IReadOnlyList<AgentName> selectedAgentNames,
        IReadOnlyList<CanonicalAgentPackage> selectedAgents,
        IReadOnlyList<CanonicalSkillPackage> resolvedSkills)
    {
        CatalogId = catalogId ?? throw new ArgumentNullException(nameof(catalogId));
        BundleVersion = bundleVersion ?? throw new ArgumentNullException(nameof(bundleVersion));
        SelectedAgentNames = CopyRequiredItems(selectedAgentNames, nameof(selectedAgentNames));
        SelectedAgents = CreateAgentSnapshot(selectedAgents, CatalogId, BundleVersion, nameof(selectedAgents));
        ResolvedSkills = CreateSkillSnapshot(resolvedSkills, CatalogId, BundleVersion, nameof(resolvedSkills));
    }

    /// <summary> Gets the catalog that owns selected agents and resolved skills. </summary>
    public AgentDistributionCatalogId CatalogId { get; }

    /// <summary> Gets the generated bundle version shared by selected packages. </summary>
    public AgentDistributionBundleVersion BundleVersion { get; }

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
        AgentDistributionCatalogId catalogId,
        AgentDistributionBundleVersion bundleVersion,
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

            if (agent.Manifest.CatalogId != catalogId
                || agent.Manifest.BundleVersion != bundleVersion)
            {
                throw new ArgumentException(
                    $"Catalog agent does not belong to the catalog and bundle version: {agent.Manifest.AgentName.Value}",
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
        AgentDistributionCatalogId catalogId,
        AgentDistributionBundleVersion bundleVersion,
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

            if (skill.Manifest.CatalogId != catalogId
                || skill.Manifest.SkillBundleVersion.Value != bundleVersion.Value)
            {
                throw new ArgumentException(
                    $"Catalog resolved skill does not belong to the catalog and bundle version: {skill.Manifest.SkillName.Value}",
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
