using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Installation.State;

/// <summary> Defines shared drift classification rules for installed target states. </summary>
internal static class SkillInstalledTargetStateKindExtensions
{
    /// <summary> Resolves a validation failure code to the stable drift state kind that represents it. </summary>
    /// <param name="code"> The failure code emitted by target validation or installed package integrity verification. </param>
    /// <param name="kind"> The resolved drift state kind when this method returns <see langword="true" />. </param>
    /// <returns> <see langword="true" /> when <paramref name="code" /> is one of the shared drift classifications; otherwise <see langword="false" />. </returns>
    public static bool TryResolveDriftKind (
        SkillFailureCode code,
        out SkillInstalledTargetStateKind kind)
    {
        return SkillInstalledTargetStateClassifier.TryResolveDriftKind(code, out kind);
    }

    /// <summary> Gets the ordering priority used when multiple drift signals are present for the same target. </summary>
    /// <param name="kind"> The target state kind to classify. </param>
    /// <returns> The drift priority where lower values win; <see cref="int.MaxValue" /> when <paramref name="kind" /> is not a local-modification drift state. </returns>
    public static int GetDriftPriority (this SkillInstalledTargetStateKind kind)
    {
        return SkillInstalledTargetStateClassifier.GetDriftPriority(kind);
    }

    /// <summary> Gets whether an install, update, or uninstall operation must treat the state as local modification drift. </summary>
    /// <param name="kind"> The target state kind to classify. </param>
    /// <returns> <see langword="true" /> when the state is a drift state that requires force to replace or delete; otherwise <see langword="false" />. </returns>
    public static bool IsLocalModificationDrift (this SkillInstalledTargetStateKind kind)
    {
        return SkillInstalledTargetStateClassifier.IsLocalModificationDrift(kind);
    }
}
