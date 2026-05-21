namespace MackySoft.AgentSkills.Installation.Diffing;

/// <summary>
/// Represents a deterministic fingerprint of the existing target directory entries read at planning time.
/// </summary>
/// <param name="Digest">
/// The SHA-256 digest, including the <c>sha256:</c> prefix, computed from slash-separated relative file paths,
/// LF-normalized file contents, and directory paths.
/// </param>
internal sealed record SkillActionTargetSnapshot (string Digest);
