using MackySoft.AgentSkills.Catalogs;
using MackySoft.AgentSkills.Hosting.Commands;
using MackySoft.AgentSkills.Tiers;

namespace MackySoft.AgentSkills.Hosting.Configuration;

/// <summary> Configures the product-owned Agent Skills command runtime. </summary>
public sealed class AgentSkillsCommandRuntimeOptions
{
    /// <summary> Gets or sets the product name written by the default command result emitter. </summary>
    public string ProductName { get; set; } = string.Empty;

    /// <summary> Gets or sets the product-owned catalog ID used by prune operations. </summary>
    public string CatalogId { get; set; } = string.Empty;

    /// <summary> Gets or sets the complete product-owned tier literal set accepted by command requests. </summary>
    public IReadOnlyList<string> Tiers { get; set; } = Array.Empty<string>();

    /// <summary> Gets or sets the application base directory that contains the bundled <c>skills</c> directory. </summary>
    public string PackageBaseDirectory { get; set; } = string.Empty;

    /// <summary> Gets or sets the public command root used in standard command result names. </summary>
    public string CommandRoot { get; set; } = AgentSkillsCommandNames.Root;

    /// <summary> Gets or sets the resolver used when a project-scope command omits its repository root. </summary>
    public Func<string, string> RepositoryRootResolver { get; set; } = static currentDirectory => currentDirectory;

    internal AgentSkillsCommandRuntimeOptions CreateValidatedCopy ()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ProductName);
        ArgumentException.ThrowIfNullOrWhiteSpace(CatalogId);
        ArgumentException.ThrowIfNullOrWhiteSpace(PackageBaseDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(CommandRoot);
        ArgumentNullException.ThrowIfNull(Tiers);
        ArgumentNullException.ThrowIfNull(RepositoryRootResolver);

        if (!SkillCatalogId.TryCreate(CatalogId, out _))
        {
            throw new ArgumentException($"SKILL catalog ID is invalid: {CatalogId}", nameof(CatalogId));
        }

        AgentSkillsCommandRootValidator.ThrowIfInvalid(CommandRoot, nameof(CommandRoot));

        var tiersResult = SkillTierLiteralParser.ParseDefinedTiers(Tiers);
        if (!tiersResult.IsSuccess)
        {
            throw new ArgumentException(tiersResult.Failure!.Message, nameof(Tiers));
        }

        return new AgentSkillsCommandRuntimeOptions
        {
            ProductName = ProductName,
            CatalogId = CatalogId,
            Tiers = Tiers.ToArray(),
            PackageBaseDirectory = Path.GetFullPath(PackageBaseDirectory),
            CommandRoot = CommandRoot,
            RepositoryRootResolver = RepositoryRootResolver,
        };
    }
}
