using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Installation.State;

/// <summary> Represents the analyzed state of one installed SKILL target. </summary>
/// <param name="Kind"> The target state kind. </param>
/// <param name="Failure"> The failure contract represented by this state, when the state is not writable without special handling. </param>
/// <param name="FileSet"> The structured file-set drift details, when <paramref name="Kind" /> is <see cref="SkillInstalledTargetStateKind.FileSetDrift" />. </param>
/// <param name="InstalledSkillBundleVersion"> The installed manifest SKILL bundle version, when a managed manifest was available. </param>
/// <param name="BundledSkillBundleVersion"> The bundled canonical package SKILL bundle version, when a canonical package was available. </param>
public sealed record SkillInstalledTargetState (
    SkillInstalledTargetStateKind Kind,
    SkillFailure? Failure = null,
    SkillInstalledTargetFileSet? FileSet = null,
    int? InstalledSkillBundleVersion = null,
    int? BundledSkillBundleVersion = null);
