using MackySoft.AgentSkills.Installation.State;

namespace MackySoft.AgentSkills.Installation.Services;

/// <summary> Centralizes force eligibility for install, update, and uninstall operations. </summary>
internal static class SkillForceTargetStatePolicy
{
    public static bool CanCreate (SkillInstalledTargetStateKind kind)
    {
        return kind == SkillInstalledTargetStateKind.Missing;
    }

    public static bool CanInstallReplace (
        SkillInstalledTargetStateKind kind,
        bool force)
    {
        return force && (kind == SkillInstalledTargetStateKind.CleanOutdated || kind.IsLocalModificationDrift());
    }

    public static bool CanUpdateReplace (
        SkillInstalledTargetStateKind kind,
        bool force)
    {
        return kind == SkillInstalledTargetStateKind.CleanOutdated || (force && kind.IsLocalModificationDrift());
    }

    public static bool CanDelete (
        SkillInstalledTargetStateKind kind,
        bool force)
    {
        return kind is SkillInstalledTargetStateKind.Current or SkillInstalledTargetStateKind.CleanOutdated
            || (force && kind.IsLocalModificationDrift());
    }
}
