using MackySoft.AgentDistribution.Materialization;
using MackySoft.AgentDistribution.Shared;
using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Installation.Contracts;

/// <summary> Represents one materialized SKILL package write request. </summary>
public sealed class SkillMaterializedPackageWriteRequest
{
    /// <summary> Initializes one materialized SKILL package write request. </summary>
    /// <param name="targetRoot"> The resolved bundle target root. </param>
    /// <param name="skillDirectory"> The resolved skill package directory. </param>
    /// <param name="materializedPackage"> The materialized package to write. </param>
    /// <param name="writeMode"> The required target existence condition at commit time. </param>
    /// <param name="precondition"> The optional validation invoked for the target path immediately before move and for the moved tree before commit. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="targetRoot" />, <paramref name="skillDirectory" />, or <paramref name="materializedPackage" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="writeMode" /> is not defined. </exception>
    public SkillMaterializedPackageWriteRequest (
        AbsolutePath targetRoot,
        AbsolutePath skillDirectory,
        SkillMaterializedPackage materializedPackage,
        SkillMaterializedPackageWriteMode writeMode,
        Func<AbsolutePath, CancellationToken, ValueTask<AgentDistributionOperationResult<bool>>>? precondition)
    {
        if (!Vocabulary.IsDefined(writeMode))
        {
            throw new ArgumentOutOfRangeException(nameof(writeMode), writeMode, "Unsupported SKILL package write mode.");
        }

        TargetRoot = targetRoot ?? throw new ArgumentNullException(nameof(targetRoot));
        SkillDirectory = skillDirectory ?? throw new ArgumentNullException(nameof(skillDirectory));
        MaterializedPackage = materializedPackage ?? throw new ArgumentNullException(nameof(materializedPackage));
        WriteMode = writeMode;
        Precondition = precondition;
    }

    /// <summary> Gets the resolved bundle target root. </summary>
    public AbsolutePath TargetRoot { get; }

    /// <summary> Gets the resolved skill package directory. </summary>
    public AbsolutePath SkillDirectory { get; }

    /// <summary> Gets the materialized package to write. </summary>
    public SkillMaterializedPackage MaterializedPackage { get; }

    /// <summary> Gets the required target existence condition at commit time. </summary>
    public SkillMaterializedPackageWriteMode WriteMode { get; }

    /// <summary> Gets the optional validation invoked before replacement and after moving the existing target. </summary>
    public Func<AbsolutePath, CancellationToken, ValueTask<AgentDistributionOperationResult<bool>>>? Precondition { get; }
}
