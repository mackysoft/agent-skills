using MackySoft.AgentDistribution.Installation.Contracts;
using MackySoft.AgentDistribution.Shared;

namespace MackySoft.AgentDistribution.Tests.Installation.Services;

/// <summary> Mutates a target once at the write boundary before delegating to the real package writer. </summary>
internal sealed class MutatingSkillMaterializedPackageWriter : ISkillMaterializedPackageWriter
{
    private readonly ISkillMaterializedPackageWriter inner;
    private readonly Action<string> mutate;

    private int mutationInvoked;

    internal MutatingSkillMaterializedPackageWriter (
        ISkillMaterializedPackageWriter inner,
        Action<string> mutate)
    {
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        this.mutate = mutate ?? throw new ArgumentNullException(nameof(mutate));
    }

    /// <inheritdoc />
    public ValueTask<AgentDistributionOperationResult<bool>> WriteAsync (
        SkillMaterializedPackageWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (Interlocked.Exchange(ref mutationInvoked, 1) == 0)
        {
            mutate(request.SkillDirectory.Value);
        }

        return inner.WriteAsync(request, cancellationToken);
    }
}
