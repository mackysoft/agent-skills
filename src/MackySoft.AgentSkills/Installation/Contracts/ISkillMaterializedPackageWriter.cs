using MackySoft.AgentSkills.Materialization;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Installation.Contracts;

/// <summary> Writes one materialized SKILL package into a resolved bundle target root. </summary>
public interface ISkillMaterializedPackageWriter
{
    /// <summary> Replaces one skill directory with a materialized package. </summary>
    /// <param name="targetRoot"> The resolved bundle target root. </param>
    /// <param name="skillDirectory"> The resolved skill package directory. </param>
    /// <param name="materializedPackage"> The materialized package to write. </param>
    /// <param name="writeMode"> The required target existence condition at commit time. </param>
    /// <param name="precondition"> The optional validation invoked for the target path immediately before move and for the moved tree before commit. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> Success when the directory is atomically replaced; otherwise a failure. </returns>
    ValueTask<SkillOperationResult<bool>> WriteAsync (
        string targetRoot,
        string skillDirectory,
        SkillMaterializedPackage materializedPackage,
        SkillMaterializedPackageWriteMode writeMode,
        Func<string, CancellationToken, ValueTask<SkillOperationResult<bool>>>? precondition,
        CancellationToken cancellationToken = default);
}
