namespace MackySoft.AgentSkills.Installation.Diffing;

/// <summary>
/// Represents a deterministic fingerprint of the existing target directory entries read at planning time.
/// </summary>
/// <param name="Digest">
/// The lowercase hexadecimal SHA-256 digest computed from slash-separated relative file paths,
/// LF-normalized file contents, and directory paths.
/// </param>
internal sealed record SkillActionTargetSnapshot (string Digest);
