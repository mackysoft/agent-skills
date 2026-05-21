namespace MackySoft.AgentSkills.Installation.Diffing;

/// <summary> Represents a deterministic fingerprint of an installed target file set. </summary>
/// <param name="Digest"> The digest computed from existing package-relative file paths and contents. </param>
internal sealed record SkillActionTargetSnapshot (string Digest);
