using MackySoft.FileSystem;

namespace MackySoft.AgentSkills.Hosting.Configuration;

/// <summary> Represents validated, immutable configuration for one Agent Skills command runtime. </summary>
public sealed class AgentSkillsCommandRuntimeConfiguration
{
    /// <summary> Initializes validated configuration for one Agent Skills command runtime. </summary>
    /// <param name="productName"> The product name written by the default command result emitter. </param>
    /// <param name="packageBaseDirectory"> The application base directory that contains the bundled <c>skills</c> directory. </param>
    /// <param name="repositoryRootResolver"> The resolver used when a project-scope command omits its repository root. </param>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="productName" /> is empty or whitespace. </exception>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="packageBaseDirectory" /> or <paramref name="repositoryRootResolver" /> is <see langword="null" />. </exception>
    public AgentSkillsCommandRuntimeConfiguration (
        string productName,
        AbsolutePath packageBaseDirectory,
        Func<AbsolutePath, AbsolutePath> repositoryRootResolver)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productName);
        ArgumentNullException.ThrowIfNull(packageBaseDirectory);
        ArgumentNullException.ThrowIfNull(repositoryRootResolver);

        ProductName = productName;
        PackageBaseDirectory = packageBaseDirectory;
        RepositoryRootResolver = repositoryRootResolver;
    }

    /// <summary> Gets the product name written by the default command result emitter. </summary>
    public string ProductName { get; }

    /// <summary> Gets the absolute application base directory that contains the bundled <c>skills</c> directory. </summary>
    public AbsolutePath PackageBaseDirectory { get; }

    /// <summary> Gets the resolver used when a project-scope command omits its repository root. </summary>
    public Func<AbsolutePath, AbsolutePath> RepositoryRootResolver { get; }
}
