using MackySoft.AgentSkills.Digests;
using MackySoft.AgentSkills.Hosts.Contracts;
using MackySoft.AgentSkills.Shared.Text;

namespace MackySoft.AgentSkills.OperationReports.Contracts;

/// <summary> Represents one host-specific artifact in a listed canonical skill. </summary>
public sealed class SkillHostArtifactReport
{
    internal SkillHostArtifactReport (
        SkillHostKind host,
        string? path,
        Sha256Digest? digest,
        Sha256Digest materializedFrontmatterDigest)
    {
        if (!ContractLiteralCodec.IsDefined(host))
        {
            throw new ArgumentOutOfRangeException(nameof(host), host, "Unsupported SKILL host.");
        }

        if ((path is null) != (digest is null))
        {
            throw new ArgumentException("Host artifact path and digest must either both be present or both be absent.");
        }

        if (path is not null)
        {
            OperationReportContractGuard.ValidateSafeRelativePath(path, nameof(path));
        }

        Host = host;
        Path = path;
        Digest = digest;
        MaterializedFrontmatterDigest = materializedFrontmatterDigest ?? throw new ArgumentNullException(nameof(materializedFrontmatterDigest));
    }

    /// <summary> Gets the host that owns the artifact. </summary>
    public SkillHostKind Host { get; }

    /// <summary> Gets the host artifact path, or <see langword="null" /> when the artifact is frontmatter-only. </summary>
    public string? Path { get; }

    /// <summary> Gets the host artifact digest, or <see langword="null" /> when no file artifact exists. </summary>
    public Sha256Digest? Digest { get; }

    /// <summary> Gets the materialized SKILL.md frontmatter digest. </summary>
    public Sha256Digest MaterializedFrontmatterDigest { get; }
}
