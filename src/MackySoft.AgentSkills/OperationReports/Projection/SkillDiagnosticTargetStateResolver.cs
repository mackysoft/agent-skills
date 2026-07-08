using MackySoft.AgentSkills.Installation.State;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.OperationReports.Projection;

/// <summary> Resolves diagnostic failure codes to the target state kind exposed by operation reports. </summary>
internal static class SkillDiagnosticTargetStateResolver
{
    /// <summary> Resolves a failure code to the target state kind that should be exposed in operation reports. </summary>
    /// <param name="code"> The failure code emitted by target analysis, validation, or doctor diagnostics. </param>
    /// <param name="kind"> The resolved target state kind when this method returns <see langword="true" />. </param>
    /// <returns> <see langword="true" /> when <paramref name="code" /> has a target state representation; otherwise <see langword="false" />. </returns>
    public static bool TryResolve (
        SkillFailureCode code,
        out SkillTargetStateKind kind)
    {
        if (code == SkillFailureCodes.InstallTargetOutdated)
        {
            kind = SkillTargetStateKind.CleanOutdated;
            return true;
        }

        if (code == SkillFailureCodes.InstallTargetVersionAhead)
        {
            kind = SkillTargetStateKind.VersionAhead;
            return true;
        }

        if (code == SkillFailureCodes.InstallTargetRemovedFromCatalog)
        {
            kind = SkillTargetStateKind.RemovedFromCatalog;
            return true;
        }

        return SkillInstalledTargetStateClassifier.TryResolveDriftKind(code, out kind);
    }
}
