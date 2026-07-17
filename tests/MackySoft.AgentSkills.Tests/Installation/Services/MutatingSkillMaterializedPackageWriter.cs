using MackySoft.AgentSkills.Installation.Contracts;
using MackySoft.AgentSkills.Materialization;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Tests.Installation.Services;

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
    public ValueTask<SkillOperationResult<bool>> WriteAsync (
        string targetRoot,
        string skillDirectory,
        SkillMaterializedPackage materializedPackage,
        SkillMaterializedPackageWriteMode writeMode,
        Func<string, CancellationToken, ValueTask<SkillOperationResult<bool>>>? precondition,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Interlocked.Exchange(ref mutationInvoked, 1) == 0)
        {
            mutate(skillDirectory);
        }

        return inner.WriteAsync(
            targetRoot,
            skillDirectory,
            materializedPackage,
            writeMode,
            precondition,
            cancellationToken);
    }
}
