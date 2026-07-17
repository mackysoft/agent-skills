using MackySoft.AgentSkills.Categories;
using MackySoft.AgentSkills.Hosts.Contracts;
using MackySoft.AgentSkills.Installation.Targeting;
using MackySoft.AgentSkills.Names;
using MackySoft.AgentSkills.OperationReports.Contracts;
using MackySoft.AgentSkills.Shared.Text;

namespace MackySoft.AgentSkills.OperationReports.Projection;

/// <summary> Provides operation context that is owned by the caller rather than stored on every result model. </summary>
public sealed class SkillOperationReportContext
{
    /// <summary> Initializes immutable context for one operation report. </summary>
    /// <param name="hostDescriptor"> The descriptor for the host used for the operation. </param>
    /// <param name="scope"> The install scope used for the operation. </param>
    /// <param name="repositoryRoot"> The canonical absolute repository root for project scope; <see langword="null" /> for user scope. </param>
    /// <param name="selectedCategories"> The selected product-owned SKILL categories. </param>
    /// <param name="selectedSkillNames"> The exact selected SKILL names. Empty means no name filter. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="hostDescriptor" />, a project-scope <paramref name="repositoryRoot" />, <paramref name="selectedCategories" />, or <paramref name="selectedSkillNames" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when the repository root does not match the selected scope, or when a selected category or SKILL name is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="scope" /> is unsupported. </exception>
    public SkillOperationReportContext (
        SkillHostDescriptor hostDescriptor,
        SkillScopeKind scope,
        string? repositoryRoot,
        IReadOnlyList<SkillCategory> selectedCategories,
        IReadOnlyList<SkillName> selectedSkillNames)
    {
        HostDescriptor = hostDescriptor ?? throw new ArgumentNullException(nameof(hostDescriptor));
        if (!ContractLiteralCodec.IsDefined(scope))
        {
            throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unsupported SKILL install scope.");
        }

        Scope = scope;
        RepositoryRoot = OperationReportContractGuard.NormalizeRepositoryRoot(scope, repositoryRoot, nameof(repositoryRoot));
        ArgumentNullException.ThrowIfNull(selectedCategories);
        ArgumentNullException.ThrowIfNull(selectedSkillNames);

        var categorySnapshot = selectedCategories.ToArray();
        if (categorySnapshot.Any(static category => category is null))
        {
            throw new ArgumentException("Selected categories must not contain null.", nameof(selectedCategories));
        }

        var skillNameSnapshot = selectedSkillNames.ToArray();
        if (skillNameSnapshot.Any(static skillName => skillName is null))
        {
            throw new ArgumentException("Selected SKILL names must not contain null.", nameof(selectedSkillNames));
        }

        SelectedCategories = Array.AsReadOnly(categorySnapshot);
        SelectedSkillNames = Array.AsReadOnly(skillNameSnapshot);
    }

    /// <summary> Gets the descriptor for the host used for the operation. </summary>
    public SkillHostDescriptor HostDescriptor { get; }

    /// <summary> Gets the install scope used for the operation. </summary>
    public SkillScopeKind Scope { get; }

    /// <summary> Gets the canonical absolute repository root for project scope, or <see langword="null" /> for user scope. </summary>
    public string? RepositoryRoot { get; }

    /// <summary> Gets selected product-owned SKILL categories. </summary>
    public IReadOnlyList<SkillCategory> SelectedCategories { get; }

    /// <summary> Gets exact selected SKILL names. Empty means no name filter. </summary>
    public IReadOnlyList<SkillName> SelectedSkillNames { get; }
}
