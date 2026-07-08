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
    public IReadOnlyList<string> DefinedTiers { get; set; } = Array.Empty<string>();

    /// <summary> Gets or sets the application base directory that contains the bundled <c>skills</c> directory. </summary>
    public string PackageBaseDirectory { get; set; } = string.Empty;

    /// <summary> Gets or sets the public command root used in standard command result names. </summary>
    public string CommandRoot { get; set; } = AgentSkillsCommandNames.Root;

    internal AgentSkillsCommandRuntimeOptions CreateValidatedCopy ()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ProductName);
        ArgumentException.ThrowIfNullOrWhiteSpace(CatalogId);
        ArgumentException.ThrowIfNullOrWhiteSpace(PackageBaseDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(CommandRoot);
        ArgumentNullException.ThrowIfNull(DefinedTiers);

        if (!SkillCatalogId.TryCreate(CatalogId, out _))
        {
            throw new ArgumentException($"SKILL catalog ID is invalid: {CatalogId}", nameof(CatalogId));
        }

        if (!IsSafeCommandRoot(CommandRoot))
        {
            throw new ArgumentException($"SKILL command root is invalid: {CommandRoot}", nameof(CommandRoot));
        }

        var tiersResult = SkillTierLiteralParser.ParseDefinedTiers(DefinedTiers);
        if (!tiersResult.IsSuccess)
        {
            throw new ArgumentException(tiersResult.Failure!.Message, nameof(DefinedTiers));
        }

        return new AgentSkillsCommandRuntimeOptions
        {
            ProductName = ProductName,
            CatalogId = CatalogId,
            DefinedTiers = DefinedTiers.ToArray(),
            PackageBaseDirectory = Path.GetFullPath(PackageBaseDirectory),
            CommandRoot = CommandRoot,
        };
    }

    private static bool IsSafeCommandRoot (string commandRoot)
    {
        var tokens = commandRoot.Split(' ', StringSplitOptions.None);
        if (tokens.Length == 0)
        {
            return false;
        }

        foreach (var token in tokens)
        {
            if (!IsSafeCommandToken(token))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsSafeCommandToken (string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        if (!IsLowerAsciiLetterOrDigit(token[0]))
        {
            return false;
        }

        var previousWasHyphen = false;
        for (var i = 1; i < token.Length; i++)
        {
            var character = token[i];
            if (character == '-')
            {
                if (previousWasHyphen)
                {
                    return false;
                }

                previousWasHyphen = true;
                continue;
            }

            if (!IsLowerAsciiLetterOrDigit(character))
            {
                return false;
            }

            previousWasHyphen = false;
        }

        return !previousWasHyphen;
    }

    private static bool IsLowerAsciiLetterOrDigit (char character)
    {
        return character is (>= 'a' and <= 'z') or (>= '0' and <= '9');
    }
}
