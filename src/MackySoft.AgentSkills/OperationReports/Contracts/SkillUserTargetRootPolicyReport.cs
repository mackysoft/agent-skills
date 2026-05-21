namespace MackySoft.AgentSkills.OperationReports.Contracts;

/// <summary> Represents product-neutral user-scope target root resolution data for one host. </summary>
/// <param name="EnvironmentVariableName"> The optional environment variable that overrides the home-relative target root. </param>
/// <param name="EnvironmentVariableChildDirectory"> The child directory appended to the environment variable value when present. </param>
/// <param name="HomeRelativeDirectory"> The home-relative fallback target directory. </param>
public sealed record SkillUserTargetRootPolicyReport (
    string? EnvironmentVariableName,
    string? EnvironmentVariableChildDirectory,
    string HomeRelativeDirectory);
