using MackySoft.AgentSkills.Installation.State;

namespace MackySoft.AgentSkills.Installation.Services;

/// <summary> Centralizes force eligibility for install, update, and uninstall operations. </summary>
internal static class SkillForceTargetStatePolicy
{
    /// <summary> Gets whether the target state allows creating a new skill directory. </summary>
    /// <param name="kind"> The analyzed target state kind. </param>
    /// <returns> <see langword="true" /> only when the target is missing. </returns>
    public static bool CanCreate (SkillInstalledTargetStateKind kind)
    {
        return kind == SkillInstalledTargetStateKind.Missing;
    }

    /// <summary> Gets whether an install action may replace the target directory. </summary>
    /// <param name="kind"> The analyzed target state kind. </param>
    /// <param name="force"> Whether install force semantics are enabled. </param>
    /// <returns>
    /// <see langword="true" /> only when force is enabled and the target is clean-outdated or a local-modification
    /// drift state.
    /// </returns>
    public static bool CanInstallReplace (
        SkillInstalledTargetStateKind kind,
        bool force)
    {
        return force
            && (kind == SkillInstalledTargetStateKind.CleanOutdated || SkillInstalledTargetStateClassifier.IsLocalModificationDrift(kind));
    }

    /// <summary> Gets whether an update action may replace the target directory. </summary>
    /// <param name="kind"> The analyzed target state kind. </param>
    /// <param name="force"> Whether update force semantics are enabled. </param>
    /// <returns>
    /// <see langword="true" /> when the target is clean-outdated, or when force is enabled and the target is a
    /// local-modification drift state.
    /// </returns>
    public static bool CanUpdateReplace (
        SkillInstalledTargetStateKind kind,
        bool force)
    {
        return kind == SkillInstalledTargetStateKind.CleanOutdated
            || (force && SkillInstalledTargetStateClassifier.IsLocalModificationDrift(kind));
    }

    /// <summary> Gets whether an uninstall action may delete the target directory. </summary>
    /// <param name="kind"> The analyzed target state kind. </param>
    /// <param name="force"> Whether uninstall force semantics are enabled. </param>
    /// <returns>
    /// <see langword="true" /> when the target is current or clean-outdated, or when force is enabled and the target
    /// is a local-modification drift state.
    /// </returns>
    public static bool CanDelete (
        SkillInstalledTargetStateKind kind,
        bool force)
    {
        return kind is SkillInstalledTargetStateKind.Current or SkillInstalledTargetStateKind.CleanOutdated
            || (force && SkillInstalledTargetStateClassifier.IsLocalModificationDrift(kind));
    }
}
