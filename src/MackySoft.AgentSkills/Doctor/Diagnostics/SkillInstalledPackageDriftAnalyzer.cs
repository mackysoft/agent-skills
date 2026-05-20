using MackySoft.AgentSkills.Installation.State;
using MackySoft.AgentSkills.Packaging.Canonical;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Doctor.Diagnostics;

/// <summary> Classifies local drift in one installed SKILL package for doctor diagnostics. </summary>
public sealed class SkillInstalledPackageDriftAnalyzer
{
    private readonly SkillInstalledTargetStateAnalyzer targetStateAnalyzer;

    /// <summary> Initializes a new instance of the <see cref="SkillInstalledPackageDriftAnalyzer" /> class. </summary>
    /// <param name="targetStateAnalyzer"> The shared installed target state analyzer. </param>
    public SkillInstalledPackageDriftAnalyzer (SkillInstalledTargetStateAnalyzer targetStateAnalyzer)
    {
        this.targetStateAnalyzer = targetStateAnalyzer ?? throw new ArgumentNullException(nameof(targetStateAnalyzer));
    }

    /// <summary> Classifies local drift for one installed skill directory. </summary>
    /// <param name="package"> The current canonical package. </param>
    /// <param name="skillDirectory"> The installed skill directory. </param>
    /// <param name="host"> The requested host. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by doctor execution. </param>
    /// <returns> The primary drift category or a read/manifest failure. </returns>
    public async ValueTask<SkillOperationResult<SkillInstalledPackageDrift>> AnalyzeAsync (
        CanonicalSkillPackage package,
        string skillDirectory,
        string host,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentException.ThrowIfNullOrWhiteSpace(skillDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        cancellationToken.ThrowIfCancellationRequested();

        var stateResult = await targetStateAnalyzer.AnalyzeAsync(package, skillDirectory, host, cancellationToken).ConfigureAwait(false);
        if (!stateResult.IsSuccess)
        {
            return SkillOperationResult<SkillInstalledPackageDrift>.FailureResult(
                stateResult.Failure!.Code,
                stateResult.Failure.Message);
        }

        var state = stateResult.Value!;
        var failure = state.Failure ?? CreateFailure(state.Kind, package.Manifest.SkillName);
        return SkillOperationResult<SkillInstalledPackageDrift>.Success(new SkillInstalledPackageDrift(failure.Code, failure.Message));
    }

    private static SkillFailure CreateFailure (
        SkillInstalledTargetStateKind kind,
        string skillName)
    {
        return kind switch
        {
            SkillInstalledTargetStateKind.Current => SkillFailure.Create(
                SkillFailureCodes.InstallTargetDigestMismatch,
                $"Installed SKILL package is current: {skillName}"),
            SkillInstalledTargetStateKind.Missing => SkillFailure.Create(
                SkillFailureCodes.InstallTargetUnmanaged,
                $"Installed SKILL package is missing: {skillName}"),
            _ => SkillFailure.Create(
                SkillFailureCodes.InstallTargetLocalModification,
                $"Installed SKILL package contains local modifications: {skillName}"),
        };
    }
}
