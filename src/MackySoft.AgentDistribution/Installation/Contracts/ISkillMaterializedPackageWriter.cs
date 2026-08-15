using MackySoft.AgentDistribution.Shared;

namespace MackySoft.AgentDistribution.Installation.Contracts;

/// <summary> Writes one materialized SKILL package into a resolved bundle target root. </summary>
public interface ISkillMaterializedPackageWriter
{
    /// <summary> Replaces one skill directory with a materialized package. </summary>
    /// <param name="request"> The resolved package write request. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> Success when the directory is atomically replaced; otherwise a failure. </returns>
    ValueTask<AgentDistributionOperationResult<bool>> WriteAsync (
        SkillMaterializedPackageWriteRequest request,
        CancellationToken cancellationToken = default);
}
