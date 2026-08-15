using MackySoft.AgentDistribution.Agents.Distribution;
using MackySoft.AgentDistribution.Agents.Doctor;
using MackySoft.AgentDistribution.Agents.Installation.Results;
using MackySoft.AgentDistribution.Agents.Installation.State;
using MackySoft.AgentDistribution.Agents.Manifests;
using MackySoft.AgentDistribution.Agents.Packaging;
using MackySoft.AgentDistribution.Distribution;
using MackySoft.AgentDistribution.Doctor;
using MackySoft.AgentDistribution.Installation.Results;
using MackySoft.AgentDistribution.OperationReports.Contracts;
using MackySoft.AgentDistribution.OperationReports.Literals;
using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.OperationReports.Projection;

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
        AbsolutePath outputPath,
        AgentPackageCatalog catalog,
        HostKind hostId,
        PackageExportFormat format)
    {
        ArgumentNullException.ThrowIfNull(outputPath);
        ArgumentNullException.ThrowIfNull(catalog);

        return new AgentExportReport(
            hostId,
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

    /// <summary> Creates product-neutral report data from a custom-agent install result. </summary>
    public static AgentOperationReport CreateInstallReport (
        AgentInstallResult result,
        AgentOperationReportContext context)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(context);

        return CreateOperationReport(
            result.ArtifactRoot.Value,
            result.StateRoot.Value,
            result.Actions,
            result.DryRun,
            result.Force,
            context,
            static action => action.AgentName,
            static action => Vocabulary.GetText(action.ActionKind),
            static action => ResolveStatus(action.ActionKind),
            static action => action.TargetStateKind,
            static action => action.Detail,
            static action => action.Diffs,
            Vocabulary.GetTexts<AgentReconcileActionKind>(),
            SkillOperationReportBuilder.CreateInstallReport(result.SkillResult, context.SkillContext));
    }

    /// <summary> Creates product-neutral report data from a custom-agent update result. </summary>
    public static AgentOperationReport CreateUpdateReport (
        AgentUpdateResult result,
        AgentOperationReportContext context)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(context);

        return CreateOperationReport(
            result.ArtifactRoot.Value,
            result.StateRoot.Value,
            result.Actions,
            result.DryRun,
            result.Force,
            context,
            static action => action.AgentName,
            static action => Vocabulary.GetText(action.ActionKind),
            static action => ResolveStatus(action.ActionKind),
            static action => action.TargetStateKind,
            static action => action.Detail,
            static action => action.Diffs,
            Vocabulary.GetTexts<AgentReconcileActionKind>(),
            SkillOperationReportBuilder.CreateUpdateReport(result.SkillResult, context.SkillContext));
    }

    /// <summary> Creates product-neutral report data from a custom-agent uninstall result. </summary>
    public static AgentOperationReport CreateUninstallReport (
        AgentUninstallResult result,
        AgentOperationReportContext context)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(context);

        return CreateOperationReport(
            result.ArtifactRoot.Value,
            result.StateRoot.Value,
            result.Actions,
            result.DryRun,
            result.Force,
            context,
            static action => action.AgentName,
            static action => Vocabulary.GetText(action.ActionKind),
            static action => ResolveStatus(action.ActionKind),
            static action => action.TargetStateKind,
            static action => action.Detail,
            static _ => null,
            Vocabulary.GetTexts<AgentRemovalActionKind>(),
            skillReport: null);
    }

    /// <summary> Creates product-neutral report data from a custom-agent prune result. </summary>
    public static AgentOperationReport CreatePruneReport (
        AgentPruneResult result,
        AgentOperationReportContext context)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(context);

        return CreateOperationReport(
            result.ArtifactRoot.Value,
            result.StateRoot.Value,
            result.Actions,
            result.DryRun,
            result.Force,
            context,
            static action => action.AgentName,
            static action => Vocabulary.GetText(action.ActionKind),
            static action => ResolveStatus(action.ActionKind),
            static action => action.TargetStateKind,
            static action => action.Detail,
            static _ => null,
            Vocabulary.GetTexts<AgentRemovalActionKind>(),
            skillReport: null);
    }

    /// <summary> Creates product-neutral report data from custom-agent and resolved-SKILL diagnostics. </summary>
    public static AgentDoctorReport CreateDoctorReport (
        AgentDoctorResult result,
        AgentOperationReportContext context)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(context);

        var diagnostics = result.Diagnostics
            .OrderBy(static diagnostic => diagnostic.AgentName.Value, StringComparer.Ordinal)
            .ThenBy(static diagnostic => Vocabulary.GetText(diagnostic.Area), StringComparer.Ordinal)
            .ThenBy(static diagnostic => diagnostic.Code.Value, StringComparer.Ordinal)
            .ThenBy(static diagnostic => diagnostic.Message, StringComparer.Ordinal)
            .Select(static diagnostic => new AgentDoctorDiagnosticReport(
                diagnostic.AgentName.Value,
                ResolveDiagnosticArea(diagnostic.Area),
                diagnostic.IsError ? SkillDoctorSeverity.Error : SkillDoctorSeverity.Info,
                diagnostic.Code.Value,
                diagnostic.Message))
            .ToArray();

        return new AgentDoctorReport(
            context.Host,
            context.SelectedAgentNames.Select(static agentName => agentName.Value).ToArray(),
            AgentOperationReportContext.ToOperationScope(context.Scope),
            context.RepositoryRoot,
            result.ArtifactRoot.Value,
            result.StateRoot.Value,
            diagnostics,
            SkillOperationReportBuilder.CreateDoctorReport(result.SkillResult, context.SkillContext));
    }

    private static AgentOperationReport CreateOperationReport<TAction> (
        string artifactRoot,
        string stateRoot,
        IReadOnlyList<TAction> actions,
        bool dryRun,
        bool force,
        AgentOperationReportContext context,
        Func<TAction, AgentName> getAgentName,
        Func<TAction, string> getAction,
        Func<TAction, OperationActionStatus> getStatus,
        Func<TAction, AgentInstalledTargetStateKind> getTargetState,
        Func<TAction, string?> getDetail,
        Func<TAction, IReadOnlyList<AgentArtifactDiff>?> getDiffs,
        IReadOnlyList<string> actionOrder,
        SkillOperationReport? skillReport)
    {
        var reports = actions
            .OrderBy(action => getAgentName(action).Value, StringComparer.Ordinal)
            .Select(action => new AgentOperationActionReport(
                getAgentName(action).Value,
                getAction(action),
                getStatus(action),
                new AgentTargetStateReport(ResolveTargetState(getTargetState(action)), getDetail(action)),
                CreateDiffs(getDiffs(action))))
            .ToArray();

        return new AgentOperationReport(
            context.Host,
            context.SelectedAgentNames.Select(static agentName => agentName.Value).ToArray(),
            AgentOperationReportContext.ToOperationScope(context.Scope),
            context.RepositoryRoot,
            artifactRoot,
            stateRoot,
            dryRun,
            force,
            reports,
            CreateCounts(actionOrder, reports, static report => report.Action),
            CreateCounts(Vocabulary.GetTexts<OperationActionStatus>(), reports, static report => Vocabulary.GetText(report.Status)),
            skillReport);
    }

    private static IReadOnlyList<OperationFileDiffReport> CreateDiffs (IReadOnlyList<AgentArtifactDiff>? diffs)
    {
        return diffs is null
            ? Array.Empty<OperationFileDiffReport>()
            : diffs
                .OrderBy(static diff => diff.RelativePath.Value, StringComparer.Ordinal)
                .Select(static diff => new OperationFileDiffReport(
                    diff.RelativePath.Value,
                    diff.BeforeContent is null ? SkillDiffChangeKind.Added : SkillDiffChangeKind.Modified,
                    diff.BeforeContent,
                    diff.AfterContent))
                .ToArray();
    }

    private static IReadOnlyList<OperationCountReport> CreateCounts<TAction> (
        IReadOnlyList<string> literalOrder,
        IReadOnlyList<TAction> actions,
        Func<TAction, string> getLiteral)
    {
        var counts = actions
            .GroupBy(getLiteral, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.Count(), StringComparer.Ordinal);
        return literalOrder
            .Select(literal => new OperationCountReport(literal, counts.GetValueOrDefault(literal)))
            .ToArray();
    }

    private static OperationActionStatus ResolveStatus (AgentReconcileActionKind kind)
    {
        return kind switch
        {
            AgentReconcileActionKind.Created or AgentReconcileActionKind.Updated => OperationActionStatus.Changed,
            AgentReconcileActionKind.NoOp => OperationActionStatus.NoOp,
            _ => OperationActionStatus.Blocked,
        };
    }

    private static OperationActionStatus ResolveStatus (AgentRemovalActionKind kind)
    {
        return kind switch
        {
            AgentRemovalActionKind.Deleted => OperationActionStatus.Changed,
            AgentRemovalActionKind.NoOp => OperationActionStatus.NoOp,
            AgentRemovalActionKind.SkippedCurrent => OperationActionStatus.Skipped,
            _ => OperationActionStatus.Blocked,
        };
    }

    private static AgentOperationTargetState ResolveTargetState (AgentInstalledTargetStateKind kind)
    {
        return kind switch
        {
            AgentInstalledTargetStateKind.Missing => AgentOperationTargetState.Missing,
            AgentInstalledTargetStateKind.Current => AgentOperationTargetState.Current,
            AgentInstalledTargetStateKind.LocallyModified => AgentOperationTargetState.LocallyModified,
            AgentInstalledTargetStateKind.Unmanaged => AgentOperationTargetState.Unmanaged,
            AgentInstalledTargetStateKind.OtherCatalog => AgentOperationTargetState.OtherCatalog,
            AgentInstalledTargetStateKind.Invalid => AgentOperationTargetState.Invalid,
            AgentInstalledTargetStateKind.CleanOutdated => AgentOperationTargetState.CleanOutdated,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported agent target state."),
        };
    }

    private static AgentDiagnosticArea ResolveDiagnosticArea (AgentDoctorDiagnosticArea area)
    {
        return area switch
        {
            AgentDoctorDiagnosticArea.Package => AgentDiagnosticArea.Package,
            AgentDoctorDiagnosticArea.HostArtifact => AgentDiagnosticArea.HostArtifact,
            AgentDoctorDiagnosticArea.TargetState => AgentDiagnosticArea.TargetState,
            _ => throw new ArgumentOutOfRangeException(nameof(area), area, "Unsupported agent diagnostic area."),
        };
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
            manifest.CatalogId.Value,
            manifest.SkillDependencies.Select(static skillName => skillName.Value).ToArray(),
            manifest.ContentDigest,
            manifest.ManifestDigest,
            manifest.HostArtifacts
                .OrderBy(static artifact => Vocabulary.GetText(artifact.HostId), StringComparer.Ordinal)
                .ThenBy(static artifact => artifact.Path.Value, StringComparer.Ordinal)
                .Select(static artifact => CreateHostArtifactReport(artifact))
                .ToArray());
    }

    private static AgentHostArtifactReport CreateHostArtifactReport (AgentHostArtifactManifest artifact)
    {
        return new AgentHostArtifactReport(artifact.HostId, artifact.Path.Value, artifact.Digest);
    }
}
