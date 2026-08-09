using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Hosting.Configuration;

/// <summary> Configures the product-owned Agent Distribution command runtime. </summary>
public sealed class AgentDistributionCommandRuntimeOptions
{
    /// <summary> Gets or sets the product name written by the default command result emitter. </summary>
    public string ProductName { get; set; } = string.Empty;

    /// <summary> Gets or sets the absolute application base directory that contains the bundled <c>skills</c> directory. This option is required. </summary>
    public AbsolutePath? PackageBaseDirectory { get; set; }

    /// <summary> Gets or sets the non-null resolver used when a project-scope command omits its repository root. </summary>
    public Func<AbsolutePath, AbsolutePath> RepositoryRootResolver { get; set; } = static currentDirectory => currentDirectory;

    internal AgentDistributionCommandRuntimeConfiguration CreateValidatedConfiguration ()
    {
        return new AgentDistributionCommandRuntimeConfiguration(
            ProductName,
            PackageBaseDirectory ?? throw new ArgumentNullException(nameof(PackageBaseDirectory)),
            RepositoryRootResolver);
    }
}
