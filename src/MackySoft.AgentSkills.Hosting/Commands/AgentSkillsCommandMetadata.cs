namespace MackySoft.AgentSkills.Hosting.Commands;

/// <summary> Provides stable command metadata for products that maintain their own command catalogs. </summary>
public static class AgentSkillsCommandMetadata
{
    /// <summary> Creates the supported <c>skills</c> subcommand literals in command registration order. </summary>
    /// <returns> A new array containing the supported subcommands. </returns>
    public static string[] CreateSubcommands ()
    {
        return
        [
            AgentSkillsCommandNames.ListSubcommand,
            AgentSkillsCommandNames.ExportSubcommand,
            AgentSkillsCommandNames.InstallSubcommand,
            AgentSkillsCommandNames.UpdateSubcommand,
            AgentSkillsCommandNames.UninstallSubcommand,
            AgentSkillsCommandNames.PruneSubcommand,
            AgentSkillsCommandNames.DoctorSubcommand,
        ];
    }

    /// <summary> Creates the stable report command names in command registration order. </summary>
    /// <returns> A new array containing the supported report command names. </returns>
    public static string[] CreateReportCommandNames (string commandRoot = AgentSkillsCommandNames.Root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandRoot);

        return
        [
            AgentSkillsCommandNames.CreateCommandName(commandRoot, AgentSkillsCommandNames.ListSubcommand),
            AgentSkillsCommandNames.CreateCommandName(commandRoot, AgentSkillsCommandNames.ExportSubcommand),
            AgentSkillsCommandNames.CreateCommandName(commandRoot, AgentSkillsCommandNames.InstallSubcommand),
            AgentSkillsCommandNames.CreateCommandName(commandRoot, AgentSkillsCommandNames.UpdateSubcommand),
            AgentSkillsCommandNames.CreateCommandName(commandRoot, AgentSkillsCommandNames.UninstallSubcommand),
            AgentSkillsCommandNames.CreateCommandName(commandRoot, AgentSkillsCommandNames.PruneSubcommand),
            AgentSkillsCommandNames.CreateCommandName(commandRoot, AgentSkillsCommandNames.DoctorSubcommand),
        ];
    }
}
