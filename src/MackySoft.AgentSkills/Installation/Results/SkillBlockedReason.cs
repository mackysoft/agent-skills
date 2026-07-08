using MackySoft.AgentSkills.Shared.Text;

namespace MackySoft.AgentSkills.Installation.Results;

/// <summary> Defines blocked action reason categories. </summary>
public enum SkillBlockedReason
{
    /// <summary> The operation would overwrite a managed target without <c>--force</c>. </summary>
    [ContractLiteral("managedOverwriteRequiresForce")]
    ManagedOverwriteRequiresForce = 0,

    /// <summary> The operation would overwrite or delete local modifications without <c>--force</c>. </summary>
    [ContractLiteral("localModificationRequiresForce")]
    LocalModificationRequiresForce = 1,

    /// <summary> The target directory is not managed by Agent Skills. </summary>
    [ContractLiteral("unmanagedTarget")]
    UnmanagedTarget = 2,

    /// <summary> The operation would overwrite a managed target generated from a newer SKILL bundle without <c>--force</c>. </summary>
    [ContractLiteral("installedVersionAhead")]
    InstalledVersionAhead = 3,
}
