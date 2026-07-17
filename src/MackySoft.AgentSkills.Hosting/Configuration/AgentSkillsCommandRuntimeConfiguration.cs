using MackySoft.AgentSkills.Hosting.Commands;

namespace MackySoft.AgentSkills.Hosting.Configuration;

/// <summary> Represents validated, immutable configuration for one Agent Skills command runtime. </summary>
public sealed class AgentSkillsCommandRuntimeConfiguration
{
    /// <summary> Initializes configuration with the default command root and repository-root policy. </summary>
    /// <param name="productName"> The product name written by the default command result emitter. </param>
    /// <param name="packageBaseDirectory"> The application base directory that contains the bundled <c>skills</c> directory. </param>
    public AgentSkillsCommandRuntimeConfiguration (
        string productName,
        string packageBaseDirectory)
        : this(
            productName,
            packageBaseDirectory,
            AgentSkillsCommandNames.Root,
            static currentDirectory => currentDirectory)
    {
    }

    /// <summary> Initializes validated configuration for one Agent Skills command runtime. </summary>
    /// <param name="productName"> The product name written by the default command result emitter. </param>
    /// <param name="packageBaseDirectory"> The application base directory that contains the bundled <c>skills</c> directory. </param>
    /// <param name="commandRoot"> The public command root used in standard command result names. </param>
    /// <param name="repositoryRootResolver"> The resolver used when a project-scope command omits its repository root. </param>
    public AgentSkillsCommandRuntimeConfiguration (
        string productName,
        string packageBaseDirectory,
        string commandRoot,
        Func<string, string> repositoryRootResolver)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productName);
        ArgumentException.ThrowIfNullOrWhiteSpace(packageBaseDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(commandRoot);
        ArgumentNullException.ThrowIfNull(repositoryRootResolver);
        AgentSkillsCommandRootValidator.ThrowIfInvalid(commandRoot, nameof(commandRoot));

        ProductName = productName;
        PackageBaseDirectory = Path.GetFullPath(packageBaseDirectory);
        CommandRoot = commandRoot;
        RepositoryRootResolver = repositoryRootResolver;
    }

    /// <summary> Gets the product name written by the default command result emitter. </summary>
    public string ProductName { get; }

    /// <summary> Gets the canonical application base directory that contains the bundled <c>skills</c> directory. </summary>
    public string PackageBaseDirectory { get; }

    /// <summary> Gets the public command root used in standard command result names. </summary>
    public string CommandRoot { get; }

    /// <summary> Gets the resolver used when a project-scope command omits its repository root. </summary>
    public Func<string, string> RepositoryRootResolver { get; }
}
