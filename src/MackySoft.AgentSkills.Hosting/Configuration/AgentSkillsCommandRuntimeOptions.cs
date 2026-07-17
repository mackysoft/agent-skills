using MackySoft.AgentSkills.Hosting.Commands;

namespace MackySoft.AgentSkills.Hosting.Configuration;

/// <summary> Configures the product-owned Agent Skills command runtime. </summary>
public sealed class AgentSkillsCommandRuntimeOptions
{
    /// <summary> Gets or sets the product name written by the default command result emitter. </summary>
    public string ProductName { get; set; } = string.Empty;

    /// <summary> Gets or sets the application base directory that contains the bundled <c>skills</c> directory. </summary>
    public string PackageBaseDirectory { get; set; } = string.Empty;

    /// <summary> Gets or sets the public command root used in standard command result names. </summary>
    public string CommandRoot { get; set; } = AgentSkillsCommandNames.Root;

    /// <summary> Gets or sets the resolver used when a project-scope command omits its repository root. </summary>
    public Func<string, string> RepositoryRootResolver { get; set; } = static currentDirectory => currentDirectory;

    internal AgentSkillsCommandRuntimeConfiguration CreateValidatedConfiguration ()
    {
        return new AgentSkillsCommandRuntimeConfiguration(
            ProductName,
            PackageBaseDirectory,
            CommandRoot,
            RepositoryRootResolver);
    }
}
