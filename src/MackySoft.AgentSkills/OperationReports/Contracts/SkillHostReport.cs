namespace MackySoft.AgentSkills.OperationReports.Contracts;

/// <summary> Represents product-neutral capability data for one supported SKILL host. </summary>
/// <param name="Host"> The canonical host key. </param>
/// <param name="SupportsProjectScope"> Whether project-scope installs are supported. </param>
/// <param name="SupportsUserScope"> Whether user-scope installs are supported. </param>
/// <param name="ProjectDefaultTargetPath"> The project-scope default target path relative to repository root, or <see langword="null" /> when project scope is not supported. </param>
/// <param name="UserDefaultTargetPath"> The user-scope default target path shown to users, or <see langword="null" /> when user scope is not supported. </param>
/// <param name="UserTargetRootPolicy"> The user-scope target root resolution policy, or <see langword="null" /> when user scope is not supported. </param>
/// <param name="RequiresMetadataArtifact"> Whether the host requires a materialized metadata artifact file in addition to SKILL.md frontmatter. </param>
/// <param name="MetadataArtifactPath"> The metadata artifact path relative to each skill directory, or <see langword="null" /> when the host uses frontmatter only. </param>
/// <param name="ReloadGuidance"> The host-specific guidance for reloading installed SKILLs. </param>
public sealed record SkillHostReport (
    string Host,
    bool SupportsProjectScope,
    bool SupportsUserScope,
    string? ProjectDefaultTargetPath,
    string? UserDefaultTargetPath,
    SkillUserTargetRootPolicyReport? UserTargetRootPolicy,
    bool RequiresMetadataArtifact,
    string? MetadataArtifactPath,
    string ReloadGuidance);
