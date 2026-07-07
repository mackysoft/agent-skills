using MackySoft.AgentSkills.Installation.Results;
using MackySoft.AgentSkills.Installation.State;

namespace MackySoft.AgentSkills.Installation.Services;

/// <summary> Projects analyzer target states into operation result DTOs. </summary>
internal static class SkillActionTargetStateProjection
{
    /// <summary> Creates an action target state snapshot from an analyzed target state. </summary>
    /// <param name="state"> The analyzed target state. Must not be <see langword="null" />. </param>
    /// <returns> A DTO containing the state kind, optional failure, and optional file-set drift details from <paramref name="state" />. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="state" /> is <see langword="null" />. </exception>
    public static SkillActionTargetState Create (SkillInstalledTargetState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var fileSet = state.FileSet is null
            ? null
            : new SkillActionTargetFileSet(
                state.FileSet.MissingFiles,
                state.FileSet.ExtraFiles,
                state.FileSet.ExtraDirectories);

        return new SkillActionTargetState(
            MapKind(state.Kind),
            state.Failure?.Code,
            state.Failure?.Message,
            fileSet,
            state.InstalledSkillBundleVersion,
            state.BundledSkillBundleVersion);
    }

    internal static SkillActionTargetStateKind MapKind (SkillInstalledTargetStateKind kind)
    {
        return kind switch
        {
            SkillInstalledTargetStateKind.Missing => SkillActionTargetStateKind.Missing,
            SkillInstalledTargetStateKind.Current => SkillActionTargetStateKind.Current,
            SkillInstalledTargetStateKind.CleanOutdated => SkillActionTargetStateKind.CleanOutdated,
            SkillInstalledTargetStateKind.LocalModified => SkillActionTargetStateKind.LocalModified,
            SkillInstalledTargetStateKind.Unmanaged => SkillActionTargetStateKind.Unmanaged,
            SkillInstalledTargetStateKind.ManifestDrift => SkillActionTargetStateKind.ManifestDrift,
            SkillInstalledTargetStateKind.CommonContentDrift => SkillActionTargetStateKind.CommonContentDrift,
            SkillInstalledTargetStateKind.FrontmatterDrift => SkillActionTargetStateKind.FrontmatterDrift,
            SkillInstalledTargetStateKind.HostArtifactDrift => SkillActionTargetStateKind.HostArtifactDrift,
            SkillInstalledTargetStateKind.FileSetDrift => SkillActionTargetStateKind.FileSetDrift,
            SkillInstalledTargetStateKind.NameCollision => SkillActionTargetStateKind.NameCollision,
            SkillInstalledTargetStateKind.HostConflict => SkillActionTargetStateKind.HostConflict,
            SkillInstalledTargetStateKind.VersionAhead => SkillActionTargetStateKind.VersionAhead,
            SkillInstalledTargetStateKind.RemovedFromCatalog => SkillActionTargetStateKind.RemovedFromCatalog,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported SKILL installed target state kind."),
        };
    }
}
