using MackySoft.AgentSkills.Categories;

namespace MackySoft.AgentSkills.Distribution;

/// <summary> Represents the number of bundled SKILL packages assigned to one product-owned category. </summary>
public sealed class SkillCategoryPackageCount
{
    /// <summary> Initializes one category package count. </summary>
    /// <param name="category"> The product-owned SKILL category. </param>
    /// <param name="packageCount"> The positive number of bundled packages assigned to <paramref name="category" />. </param>
    internal SkillCategoryPackageCount (
        SkillCategory category,
        int packageCount)
    {
        if (packageCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(packageCount), packageCount, "Category package count must be positive.");
        }

        Category = category ?? throw new ArgumentNullException(nameof(category));
        PackageCount = packageCount;
    }

    /// <summary> Gets the product-owned SKILL category. </summary>
    public SkillCategory Category { get; }

    /// <summary> Gets the positive number of bundled packages assigned to the category. </summary>
    public int PackageCount { get; }
}
