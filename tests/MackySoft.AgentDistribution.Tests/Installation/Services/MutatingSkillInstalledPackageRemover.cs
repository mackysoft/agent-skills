using MackySoft.AgentDistribution.Installation.Contracts;
using MackySoft.AgentDistribution.Shared;

namespace MackySoft.AgentDistribution.Tests.Installation.Services;

/// <summary> Mutates a target once at the delete boundary before delegating to the real package remover. </summary>
internal sealed class MutatingSkillInstalledPackageRemover : ISkillInstalledPackageRemover
{
    private readonly ISkillInstalledPackageRemover inner;
    private readonly Action<string> mutate;

    private int mutationInvoked;

    internal MutatingSkillInstalledPackageRemover (
        ISkillInstalledPackageRemover inner,
        Action<string> mutate)
    {
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        this.mutate = mutate ?? throw new ArgumentNullException(nameof(mutate));
    }

    /// <inheritdoc />
    public ValueTask<AgentDistributionOperationResult<bool>> DeleteAsync (
        AbsolutePath targetRoot,
        AbsolutePath skillDirectory,
        Func<AbsolutePath, CancellationToken, ValueTask<AgentDistributionOperationResult<bool>>>? precondition,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Interlocked.Exchange(ref mutationInvoked, 1) == 0)
        {
            mutate(skillDirectory.Value);
        }

        return inner.DeleteAsync(
            targetRoot,
            skillDirectory,
            precondition,
            cancellationToken);
    }
}
