namespace MackySoft.AgentSkills.OperationReports.Contracts;

/// <summary> Represents product-neutral user-scope target root resolution data for one host. </summary>
public sealed class SkillUserTargetRootPolicyReport
{
    internal SkillUserTargetRootPolicyReport (
        string? environmentVariableName,
        string? environmentVariableChildDirectory,
        string homeRelativeDirectory)
    {
        OperationReportContractGuard.ValidateOptionalText(environmentVariableName, nameof(environmentVariableName));
        OperationReportContractGuard.ValidateOptionalText(environmentVariableChildDirectory, nameof(environmentVariableChildDirectory));
        if (environmentVariableChildDirectory is not null)
        {
            if (environmentVariableName is null)
            {
                throw new ArgumentException("An environment variable child directory requires an environment variable name.", nameof(environmentVariableChildDirectory));
            }

            OperationReportContractGuard.ValidateSafeRelativePath(environmentVariableChildDirectory, nameof(environmentVariableChildDirectory));
        }

        OperationReportContractGuard.ValidateSafeRelativePath(homeRelativeDirectory, nameof(homeRelativeDirectory));

        EnvironmentVariableName = environmentVariableName;
        EnvironmentVariableChildDirectory = environmentVariableChildDirectory;
        HomeRelativeDirectory = homeRelativeDirectory;
    }

    /// <summary> Gets the optional environment variable that overrides the home-relative target root. </summary>
    public string? EnvironmentVariableName { get; }

    /// <summary> Gets the child directory appended to the environment variable value when present. </summary>
    public string? EnvironmentVariableChildDirectory { get; }

    /// <summary> Gets the home-relative fallback target directory. </summary>
    public string HomeRelativeDirectory { get; }
}
