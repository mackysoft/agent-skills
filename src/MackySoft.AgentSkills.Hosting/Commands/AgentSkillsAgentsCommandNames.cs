namespace MackySoft.AgentSkills.Hosting.Commands;

/// <summary> Provides stable command literals used by the standard custom-agent command runtime. </summary>
public static class AgentSkillsAgentsCommandNames
{
    /// <summary> The custom-agent root command group. </summary>
    public const string Root = "agents";

    /// <summary> The list subcommand. </summary>
    public const string ListSubcommand = "list";

    /// <summary> The export subcommand. </summary>
    public const string ExportSubcommand = "export";

    /// <summary> The install subcommand. </summary>
    public const string InstallSubcommand = "install";

    /// <summary> The update subcommand. </summary>
    public const string UpdateSubcommand = "update";

    /// <summary> The uninstall subcommand. </summary>
    public const string UninstallSubcommand = "uninstall";

    /// <summary> The prune subcommand. </summary>
    public const string PruneSubcommand = "prune";

    /// <summary> The doctor subcommand. </summary>
    public const string DoctorSubcommand = "doctor";

    /// <summary> Creates a command result name from a root command and subcommand. </summary>
    /// <param name="root"> The public command root. </param>
    /// <param name="subcommand"> The public subcommand. </param>
    /// <returns> The stable command result name. </returns>
    public static string CreateCommandName (
        string root,
        string subcommand)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentException.ThrowIfNullOrWhiteSpace(subcommand);

        return $"{AgentSkillsCommandRootValidator.CreateReportRoot(root)}.{subcommand}";
    }
}
