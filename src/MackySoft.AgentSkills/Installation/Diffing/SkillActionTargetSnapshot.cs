using MackySoft.AgentSkills.Digests;

namespace MackySoft.AgentSkills.Installation.Diffing;

/// <summary>
/// Represents a deterministic fingerprint of the existing target directory entries read at planning time.
/// </summary>
internal sealed record SkillActionTargetSnapshot
{
    internal SkillActionTargetSnapshot (Sha256Digest digest)
    {
        Digest = digest ?? throw new ArgumentNullException(nameof(digest));
    }

    /// <summary>
    /// Gets the SHA-256 digest computed from slash-separated relative file paths, LF-normalized file contents,
    /// and directory paths.
    /// </summary>
    internal Sha256Digest Digest { get; }
}
