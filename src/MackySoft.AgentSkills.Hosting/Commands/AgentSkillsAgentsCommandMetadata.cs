namespace MackySoft.AgentSkills.Hosting.Commands;

/// <summary> Provides stable custom-agent command metadata for products that maintain their own command catalogs. </summary>
public static class AgentSkillsAgentsCommandMetadata
{
    /// <summary> Creates the supported <c>agents</c> subcommand literals in command registration order. </summary>
    /// <returns> A new array containing the supported subcommands. </returns>
    public static string[] CreateSubcommands ()
    {
        return
        [
            AgentSkillsAgentsCommandNames.ListSubcommand,
            AgentSkillsAgentsCommandNames.ExportSubcommand,
            AgentSkillsAgentsCommandNames.InstallSubcommand,
            AgentSkillsAgentsCommandNames.UpdateSubcommand,
            AgentSkillsAgentsCommandNames.UninstallSubcommand,
            AgentSkillsAgentsCommandNames.PruneSubcommand,
            AgentSkillsAgentsCommandNames.DoctorSubcommand,
        ];
    }

    /// <summary> Creates stable custom-agent report command names in command registration order. </summary>
    /// <param name="commandRoot"> The public custom-agent command root. </param>
    /// <returns> A new array containing the supported report command names. </returns>
    public static string[] CreateReportCommandNames (string commandRoot = AgentSkillsAgentsCommandNames.Root)
    {
        AgentSkillsCommandRootValidator.ThrowIfInvalid(commandRoot, nameof(commandRoot));

        return
        [
            AgentSkillsAgentsCommandNames.CreateCommandName(commandRoot, AgentSkillsAgentsCommandNames.ListSubcommand),
            AgentSkillsAgentsCommandNames.CreateCommandName(commandRoot, AgentSkillsAgentsCommandNames.ExportSubcommand),
            AgentSkillsAgentsCommandNames.CreateCommandName(commandRoot, AgentSkillsAgentsCommandNames.InstallSubcommand),
            AgentSkillsAgentsCommandNames.CreateCommandName(commandRoot, AgentSkillsAgentsCommandNames.UpdateSubcommand),
            AgentSkillsAgentsCommandNames.CreateCommandName(commandRoot, AgentSkillsAgentsCommandNames.UninstallSubcommand),
            AgentSkillsAgentsCommandNames.CreateCommandName(commandRoot, AgentSkillsAgentsCommandNames.PruneSubcommand),
            AgentSkillsAgentsCommandNames.CreateCommandName(commandRoot, AgentSkillsAgentsCommandNames.DoctorSubcommand),
        ];
    }
}
