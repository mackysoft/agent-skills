using MackySoft.AgentSkills.Installation.Results;
using MackySoft.AgentSkills.Installation.State;

namespace MackySoft.AgentSkills.Installation.Services;

internal static class SkillActionTargetStateProjection
{
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
            state.Kind.ToString(),
            state.Failure?.Code,
            state.Failure?.Message,
            fileSet);
    }
}
