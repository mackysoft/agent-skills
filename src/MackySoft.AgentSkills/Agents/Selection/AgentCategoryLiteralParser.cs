using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Agents.Selection;

/// <summary> Parses selected custom-agent category literals against a generated bundle. </summary>
public static class AgentCategoryLiteralParser
{
    /// <summary> Parses optional category literals without requiring them to exist in the current bundle. </summary>
    /// <param name="selectedCategoryLiterals"> The category literals selected by the caller. </param>
    /// <returns> A deduplicated immutable category selection, or an input failure. </returns>
    public static SkillOperationResult<IReadOnlyList<AgentCategory>> ParseOptionalCategories (
        IReadOnlyList<string> selectedCategoryLiterals)
    {
        ArgumentNullException.ThrowIfNull(selectedCategoryLiterals);

        var selectedCategories = new List<AgentCategory>(selectedCategoryLiterals.Count);
        var selectedCategorySet = new HashSet<AgentCategory>();
        foreach (var literal in selectedCategoryLiterals)
        {
            if (!AgentCategory.TryCreate(literal, out var category))
            {
                return SkillOperationResult<IReadOnlyList<AgentCategory>>.FailureResult(
                    SkillFailureCodes.InputInvalid,
                    $"Agent category literal is invalid: {literal ?? "<null>"}.");
            }

            if (selectedCategorySet.Add(category))
            {
                selectedCategories.Add(category);
            }
        }

        return SkillOperationResult<IReadOnlyList<AgentCategory>>.Success(
            Array.AsReadOnly(selectedCategories.ToArray()));
    }

    /// <summary> Parses selected category literals and verifies they are available. </summary>
    /// <param name="availableCategories"> The complete category set present in the generated bundle. </param>
    /// <param name="selectedCategoryLiterals"> The selected category literals. </param>
    /// <returns> A deduplicated immutable category selection, or an input failure. </returns>
    public static SkillOperationResult<IReadOnlyList<AgentCategory>> ParseSelectedCategories (
        IReadOnlyList<AgentCategory> availableCategories,
        IReadOnlyList<string> selectedCategoryLiterals)
    {
        ArgumentNullException.ThrowIfNull(availableCategories);
        ArgumentNullException.ThrowIfNull(selectedCategoryLiterals);

        var availableCategorySet = new HashSet<AgentCategory>();
        foreach (var availableCategory in availableCategories)
        {
            ArgumentNullException.ThrowIfNull(availableCategory);
            availableCategorySet.Add(availableCategory);
        }

        var parsedResult = ParseOptionalCategories(selectedCategoryLiterals);
        if (!parsedResult.IsSuccess)
        {
            return parsedResult;
        }

        foreach (var category in parsedResult.Value!)
        {
            if (!availableCategorySet.Contains(category))
            {
                return SkillOperationResult<IReadOnlyList<AgentCategory>>.FailureResult(
                    SkillFailureCodes.InputInvalid,
                    $"Unsupported agent category: {category.Value}. Supported categories: {string.Join(", ", availableCategories.Select(static item => item.Value))}.");
            }
        }

        return parsedResult;
    }
}
