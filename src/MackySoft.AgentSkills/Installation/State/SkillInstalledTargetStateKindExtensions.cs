namespace MackySoft.AgentSkills.Installation.State;

internal static class SkillInstalledTargetStateKindExtensions
{
    public static bool IsLocalModificationDrift (this SkillInstalledTargetStateKind kind)
    {
        return kind is SkillInstalledTargetStateKind.LocalModified
            or SkillInstalledTargetStateKind.ManifestDrift
            or SkillInstalledTargetStateKind.CommonContentDrift
            or SkillInstalledTargetStateKind.FrontmatterDrift
            or SkillInstalledTargetStateKind.HostArtifactDrift
            or SkillInstalledTargetStateKind.FileSetDrift;
    }
}
