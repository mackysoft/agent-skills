using MackySoft.AgentSkills.Shared.Text;

namespace MackySoft.AgentSkills.Hosts.Contracts;

/// <summary> Defines how one host organizes bundle targets under its default SKILL root. </summary>
public enum SkillBundleTargetRootLayout
{
    /// <summary> Every skill is installed directly under the host SKILL root. </summary>
    [ContractLiteral("flat")]
    Flat = 0,

    /// <summary> Every bundle owns a catalog-ID directory under the host SKILL root. </summary>
    [ContractLiteral("catalog-directory")]
    CatalogDirectory = 1,
}
