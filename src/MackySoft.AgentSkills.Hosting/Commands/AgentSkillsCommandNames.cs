namespace MackySoft.AgentSkills.Hosting.Commands;

/// <summary> Provides stable command literals used by the standard Agent Skills command runtime. </summary>
public static class AgentSkillsCommandNames
{
    /// <summary> The root command group. </summary>
    public const string Root = "skills";

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

    /// <summary> The list command. </summary>
    public const string List = "skills.list";

    /// <summary> The export command. </summary>
    public const string Export = "skills.export";

    /// <summary> The install command. </summary>
    public const string Install = "skills.install";

    /// <summary> The update command. </summary>
    public const string Update = "skills.update";

    /// <summary> The uninstall command. </summary>
    public const string Uninstall = "skills.uninstall";

    /// <summary> The prune command. </summary>
    public const string Prune = "skills.prune";

    /// <summary> The doctor command. </summary>
    public const string Doctor = "skills.doctor";

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

        var reportRoot = string.Join(
            ".",
            root.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return $"{reportRoot}.{subcommand}";
    }
}
