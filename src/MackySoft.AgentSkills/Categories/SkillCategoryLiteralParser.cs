using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Categories;

/// <summary> Parses product-owned SKILL category literals. </summary>
public static class SkillCategoryLiteralParser
{
    /// <summary> Parses selected category literals without requiring them to exist in the current bundle. </summary>
    /// <param name="selectedCategoryLiterals"> The selected category literals. </param>
    /// <returns> An immutable snapshot of the validated, deduplicated categories, or an input failure. </returns>
    public static SkillOperationResult<IReadOnlyList<SkillCategory>> ParseSelectedCategories (
        IReadOnlyList<string> selectedCategoryLiterals)
    {
        ArgumentNullException.ThrowIfNull(selectedCategoryLiterals);

        if (selectedCategoryLiterals.Count == 0)
        {
            return SkillOperationResult<IReadOnlyList<SkillCategory>>.FailureResult(
                SkillFailureCodes.InputInvalid,
                "At least one SKILL category must be selected.");
        }

        var selectedCategories = new List<SkillCategory>(selectedCategoryLiterals.Count);
        var selectedCategorySet = new HashSet<SkillCategory>();
        foreach (var literal in selectedCategoryLiterals)
        {
            if (!SkillCategory.TryCreate(literal, out var category) || category is null)
            {
                return SkillOperationResult<IReadOnlyList<SkillCategory>>.FailureResult(
                    SkillFailureCodes.InputInvalid,
                    $"SKILL category literal is invalid: {literal ?? "<null>"}.");
            }

            if (selectedCategorySet.Add(category))
            {
                selectedCategories.Add(category);
            }
        }

        return SkillOperationResult<IReadOnlyList<SkillCategory>>.Success(
            Array.AsReadOnly(selectedCategories.ToArray()));
    }

    /// <summary> Parses selected category literals against available categories. </summary>
    /// <param name="availableCategories"> The complete available category set. </param>
    /// <param name="selectedCategoryLiterals"> The selected category literals. </param>
    /// <returns> An immutable snapshot of the normalized selected categories, or an input failure. </returns>
    public static SkillOperationResult<IReadOnlyList<SkillCategory>> ParseSelectedCategories (
        IReadOnlyList<SkillCategory> availableCategories,
        IReadOnlyList<string> selectedCategoryLiterals)
    {
        ArgumentNullException.ThrowIfNull(availableCategories);
        ArgumentNullException.ThrowIfNull(selectedCategoryLiterals);

        if (selectedCategoryLiterals.Count == 0)
        {
            return SkillOperationResult<IReadOnlyList<SkillCategory>>.FailureResult(
                SkillFailureCodes.InputInvalid,
                "At least one SKILL category must be selected.");
        }

        var availableCategorySet = new HashSet<SkillCategory>();
        foreach (var availableCategory in availableCategories)
        {
            ArgumentNullException.ThrowIfNull(availableCategory);
            availableCategorySet.Add(availableCategory);
        }

        var selectedCategories = new List<SkillCategory>(selectedCategoryLiterals.Count);
        var selectedCategorySet = new HashSet<SkillCategory>();
        foreach (var literal in selectedCategoryLiterals)
        {
            if (!SkillCategory.TryCreate(literal, out var category)
                || category is null
                || !availableCategorySet.Contains(category))
            {
                return SkillOperationResult<IReadOnlyList<SkillCategory>>.FailureResult(
                    SkillFailureCodes.InputInvalid,
                    $"Unsupported SKILL category: {literal ?? "<null>"}. Supported categories: {string.Join(", ", availableCategories.Select(static item => item.Value))}.");
            }

            if (selectedCategorySet.Add(category))
            {
                selectedCategories.Add(category);
            }
        }

        return SkillOperationResult<IReadOnlyList<SkillCategory>>.Success(
            Array.AsReadOnly(selectedCategories.ToArray()));
    }
}
