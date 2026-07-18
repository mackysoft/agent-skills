using MackySoft.AgentSkills.Hosts.Contracts;
using MackySoft.AgentSkills.Hosts.Registration;
using MackySoft.AgentSkills.Installation.State;
using MackySoft.AgentSkills.Packaging.Canonical;
using MackySoft.AgentSkills.Packaging.FileSystem;
using MackySoft.AgentSkills.Shared;
using MackySoft.AgentSkills.Shared.Text;

namespace MackySoft.AgentSkills.Doctor;

/// <summary> Diagnoses host-materialized SKILL package directories. </summary>
public sealed class SkillDoctorService
{
    private readonly SkillHostAdapterSet hostAdapters;
    private readonly SkillInstalledTargetStateAnalyzer targetStateAnalyzer;

    /// <summary> Initializes a new instance of the <see cref="SkillDoctorService" /> class. </summary>
    /// <param name="hostAdapters"> The supported host adapter set. </param>
    /// <param name="targetStateAnalyzer"> The installed target state analyzer. </param>
    public SkillDoctorService (
        SkillHostAdapterSet hostAdapters,
        SkillInstalledTargetStateAnalyzer targetStateAnalyzer)
    {
        this.hostAdapters = hostAdapters ?? throw new ArgumentNullException(nameof(hostAdapters));
        this.targetStateAnalyzer = targetStateAnalyzer ?? throw new ArgumentNullException(nameof(targetStateAnalyzer));
    }

    /// <summary> Diagnoses one bundle target root against canonical packages. </summary>
    /// <param name="packages"> The canonical packages. </param>
    /// <param name="host"> The target host. </param>
    /// <param name="targetRoot"> The bundle target root. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The doctor result. </returns>
    public async ValueTask<SkillDoctorResult> DiagnoseAsync (
        IReadOnlyList<CanonicalSkillPackage> packages,
        SkillHostKind host,
        string targetRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(packages);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetRoot);
        if (!ContractLiteralCodec.IsDefined(host))
        {
            throw new ArgumentOutOfRangeException(nameof(host), host, "Unsupported SKILL host.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var diagnostics = new List<SkillDoctorDiagnostic>();
        var fullTargetRoot = Path.GetFullPath(targetRoot);
        var adapterResult = hostAdapters.GetAdapter(host);
        var registeredHost = adapterResult.Value!.Descriptor.Host;
        if (!Directory.Exists(fullTargetRoot))
        {
            diagnostics.Add(SkillDoctorDiagnostic.Error(
                SkillFailureCodes.InstallTargetUnmanaged,
                $"Target root does not exist: {fullTargetRoot}"));
            return new SkillDoctorResult(registeredHost, fullTargetRoot, diagnostics);
        }

        foreach (var package in packages.OrderBy(static package => package.Manifest.SkillName.Value, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await DiagnosePackageAsync(package, registeredHost, fullTargetRoot, diagnostics, cancellationToken).ConfigureAwait(false);
        }

        if (diagnostics.Count == 0)
        {
            diagnostics.Add(SkillDoctorDiagnostic.Info(
                "SKILL_DOCTOR_OK",
                "All SKILL packages are installed for the requested host."));
        }

        return new SkillDoctorResult(registeredHost, fullTargetRoot, diagnostics);
    }

    private async ValueTask DiagnosePackageAsync (
        CanonicalSkillPackage package,
        SkillHostKind host,
        string targetRoot,
        List<SkillDoctorDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        var skillDirectoryResult = SkillPackagePathBoundary.ResolvePackageDirectory(targetRoot, package.Manifest.SkillName.Value);
        if (!skillDirectoryResult.IsSuccess)
        {
            diagnostics.Add(SkillDoctorDiagnostic.Error(skillDirectoryResult.Failure!.Code, skillDirectoryResult.Failure.Message, package.Manifest.SkillName));
            return;
        }

        var skillDirectory = skillDirectoryResult.Value!;
        if (!Directory.Exists(skillDirectory))
        {
            diagnostics.Add(SkillDoctorDiagnostic.Error(SkillFailureCodes.InstallTargetUnmanaged, "Skill directory is missing.", package.Manifest.SkillName));
            return;
        }

        var stateResult = await targetStateAnalyzer.AnalyzeAsync(package, skillDirectory, host, cancellationToken).ConfigureAwait(false);
        if (!stateResult.IsSuccess)
        {
            diagnostics.Add(SkillDoctorDiagnostic.Error(stateResult.Failure!.Code, stateResult.Failure.Message, package.Manifest.SkillName));
            return;
        }

        var state = stateResult.Value!;
        if (state.Kind == SkillTargetStateKind.Current)
        {
            return;
        }

        if (state.Kind == SkillTargetStateKind.Missing)
        {
            diagnostics.Add(SkillDoctorDiagnostic.Error(SkillFailureCodes.InstallTargetUnmanaged, "Skill directory is missing.", package.Manifest.SkillName));
            return;
        }

        var failure = state.Failure!;
        diagnostics.Add(SkillDoctorDiagnostic.Error(
            failure.Code,
            failure.Message,
            package.Manifest.SkillName));
    }
}
