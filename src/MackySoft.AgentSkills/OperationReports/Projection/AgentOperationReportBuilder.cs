using MackySoft.AgentSkills.Agents.Manifests;
using MackySoft.AgentSkills.Agents.Packaging;
using MackySoft.AgentSkills.Distribution;
using MackySoft.AgentSkills.OperationReports.Contracts;

namespace MackySoft.AgentSkills.OperationReports.Projection;

/// <summary> Builds product-neutral reports from custom-agent distribution models. </summary>
public static class AgentOperationReportBuilder
{
    /// <summary> Creates list report data from a validated selected-agent catalog. </summary>
    /// <param name="catalog"> The selected agents and resolved SKILL dependencies. </param>
    /// <returns> A deterministic report sorted by canonical names and host identifiers. </returns>
    public static AgentListReport CreateListReport (AgentPackageCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        var agents = catalog.SelectedAgents
            .OrderBy(static agent => agent.Manifest.AgentName.Value, StringComparer.Ordinal)
            .Select(static agent => CreateAgentReport(agent))
            .ToArray();
        var supportedHostIds = catalog.SelectedAgents
            .SelectMany(static agent => agent.Manifest.HostArtifacts)
            .Select(static artifact => artifact.HostId)
            .Distinct()
            .OrderBy(Vocabulary.GetText, StringComparer.Ordinal)
            .ToArray();

        return new AgentListReport(
            catalog.SelectedCategories.Select(static category => category.Value).ToArray(),
            catalog.SelectedAgentNames.Select(static agentName => agentName.Value).ToArray(),
            agents,
            catalog.ResolvedSkills
                .Select(static skill => skill.Manifest.SkillName.Value)
                .Order(StringComparer.Ordinal)
                .ToArray(),
            supportedHostIds);
    }

    /// <summary> Creates export report data from a successful custom-agent export. </summary>
    /// <param name="outputPath"> The output path returned by <see cref="AgentExportService" />. </param>
    /// <param name="catalog"> The exported selected-agent catalog. </param>
    /// <param name="hostId"> The host used for export. </param>
    /// <param name="format"> The export format. </param>
    /// <returns> A deterministic export report. </returns>
    public static AgentExportReport CreateExportReport (
        string outputPath,
        AgentPackageCatalog catalog,
        AgentHostKind hostId,
        SkillExportFormat format)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(catalog);

        return new AgentExportReport(
            hostId,
            catalog.SelectedCategories.Select(static category => category.Value).ToArray(),
            catalog.SelectedAgentNames.Select(static agentName => agentName.Value).ToArray(),
            format,
            outputPath,
            catalog.SelectedAgents
                .Select(static agent => agent.Manifest.AgentName.Value)
                .Order(StringComparer.Ordinal)
                .ToArray(),
            catalog.ResolvedSkills
                .Select(static skill => skill.Manifest.SkillName.Value)
                .Order(StringComparer.Ordinal)
                .ToArray());
    }

    private static AgentListAgentReport CreateAgentReport (CanonicalAgentPackage package)
    {
        var manifest = package.Manifest;
        return new AgentListAgentReport(
            manifest.SchemaVersion,
            manifest.BundleVersion.Value,
            manifest.AgentName.Value,
            manifest.DisplayName,
            manifest.Description,
            manifest.Category.Value,
            manifest.CatalogId.Value,
            manifest.SkillDependencies.Select(static skillName => skillName.Value).ToArray(),
            manifest.ContentDigest,
            manifest.ManifestDigest,
            manifest.HostArtifacts
                .OrderBy(static artifact => Vocabulary.GetText(artifact.HostId), StringComparer.Ordinal)
                .ThenBy(static artifact => artifact.Path, StringComparer.Ordinal)
                .Select(static artifact => CreateHostArtifactReport(artifact))
                .ToArray());
    }

    private static AgentHostArtifactReport CreateHostArtifactReport (AgentHostArtifactManifest artifact)
    {
        return new AgentHostArtifactReport(artifact.HostId, artifact.Path, artifact.Digest);
    }
}
