using MackySoft.AgentSkills.Hosts.Contracts;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Hosts.Registration;

/// <summary> Provides the deterministic global host adapter set. </summary>
public sealed class SkillHostAdapterSet
{
    private readonly ISkillHostAdapter[] adapters;

    /// <summary> Initializes a new instance of the <see cref="SkillHostAdapterSet" /> class. </summary>
    /// <param name="adapters"> The supported host adapters. </param>
    public SkillHostAdapterSet (IEnumerable<ISkillHostAdapter> adapters)
    {
        ArgumentNullException.ThrowIfNull(adapters);

        var adapterArray = adapters
            .Select(static adapter => adapter ?? throw new ArgumentException("Host adapter collection must not contain null.", nameof(adapters)))
            .ToArray();

        if (adapterArray.Length == 0)
        {
            throw new ArgumentException("At least one host adapter must be provided.", nameof(adapters));
        }

        foreach (var adapter in adapterArray)
        {
            ValidateDescriptor(adapter.Descriptor, nameof(adapters));
        }

        var duplicateHost = adapterArray
            .GroupBy(static adapter => adapter.Descriptor.HostKey, StringComparer.OrdinalIgnoreCase)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .FirstOrDefault();

        if (duplicateHost is not null)
        {
            throw new ArgumentException($"Host adapter key must be unique: {duplicateHost}", nameof(adapters));
        }

        this.adapters = adapterArray
            .OrderBy(static adapter => adapter.Descriptor.HostKey, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary> Gets all host adapters in deterministic order. </summary>
    public IReadOnlyList<ISkillHostAdapter> Adapters => adapters;

    /// <summary> Gets one adapter by host key. </summary>
    /// <param name="host"> The host key. </param>
    /// <returns> The adapter or unsupported-host failure. </returns>
    public SkillOperationResult<ISkillHostAdapter> GetAdapter (string host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return SkillOperationResult<ISkillHostAdapter>.FailureResult(
                SkillFailureCodes.HostUnsupported,
                $"Unsupported SKILL host: {host ?? "(null)"}");
        }

        foreach (var adapter in adapters)
        {
            if (string.Equals(adapter.Descriptor.HostKey, host, StringComparison.OrdinalIgnoreCase))
            {
                return SkillOperationResult<ISkillHostAdapter>.Success(adapter);
            }
        }

        return SkillOperationResult<ISkillHostAdapter>.FailureResult(
            SkillFailureCodes.HostUnsupported,
            $"Unsupported SKILL host: {host}");
    }

    private static void ValidateDescriptor (
        SkillHostDescriptor descriptor,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(descriptor, parameterName);
        ArgumentException.ThrowIfNullOrWhiteSpace(descriptor.HostKey, parameterName);
        ValidateProjectScope(descriptor, parameterName);
        ValidateUserScope(descriptor, parameterName);
        ValidateMetadataArtifact(descriptor, parameterName);
        ArgumentException.ThrowIfNullOrWhiteSpace(descriptor.ReloadGuidance, parameterName);
    }

    private static void ValidateProjectScope (
        SkillHostDescriptor descriptor,
        string parameterName)
    {
        if (!descriptor.SupportsProjectScope)
        {
            if (descriptor.ProjectDefaultTargetPath is not null)
            {
                throw new ArgumentException("Host adapter project default target path must be null when project scope is not supported.", parameterName);
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(descriptor.ProjectDefaultTargetPath))
        {
            throw new ArgumentException("Host adapter project default target path is required when project scope is supported.", parameterName);
        }

        if (!SkillRelativePath.IsSafeFilePath(descriptor.ProjectDefaultTargetPath))
        {
            throw new ArgumentException("Host adapter project default target path must be a safe relative path.", parameterName);
        }
    }

    private static void ValidateUserScope (
        SkillHostDescriptor descriptor,
        string parameterName)
    {
        if (!descriptor.SupportsUserScope)
        {
            if (descriptor.UserDefaultTargetPath is not null || descriptor.UserTargetRootPolicy is not null)
            {
                throw new ArgumentException("Host adapter user default target metadata must be null when user scope is not supported.", parameterName);
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(descriptor.UserDefaultTargetPath))
        {
            throw new ArgumentException("Host adapter user default target path is required when user scope is supported.", parameterName);
        }

        if (descriptor.UserTargetRootPolicy is null)
        {
            throw new ArgumentException("Host adapter user target root policy is required when user scope is supported.", parameterName);
        }

        ValidateUserTargetRootPolicy(descriptor.UserTargetRootPolicy, parameterName);
    }

    private static void ValidateUserTargetRootPolicy (
        SkillUserTargetRootPolicy policy,
        string parameterName)
    {
        if (!SkillRelativePath.IsSafeFilePath(policy.HomeRelativeDirectory))
        {
            throw new ArgumentException("Host adapter home-relative user target directory must be a safe relative path.", parameterName);
        }

        if (string.IsNullOrWhiteSpace(policy.EnvironmentVariableName)
            && !string.IsNullOrWhiteSpace(policy.EnvironmentVariableChildDirectory))
        {
            throw new ArgumentException("Host adapter environment variable child directory requires an environment variable name.", parameterName);
        }

        if (!string.IsNullOrWhiteSpace(policy.EnvironmentVariableChildDirectory)
            && !SkillRelativePath.IsSafeFilePath(policy.EnvironmentVariableChildDirectory))
        {
            throw new ArgumentException("Host adapter environment variable child directory must be a safe relative path.", parameterName);
        }
    }

    private static void ValidateMetadataArtifact (
        SkillHostDescriptor descriptor,
        string parameterName)
    {
        if (!descriptor.RequiresMetadataArtifact)
        {
            if (descriptor.MetadataArtifactPath is not null)
            {
                throw new ArgumentException("Host adapter metadata artifact path must be null when metadata artifacts are not required.", parameterName);
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(descriptor.MetadataArtifactPath))
        {
            throw new ArgumentException("Host adapter metadata artifact path is required when metadata artifacts are required.", parameterName);
        }

        if (!SkillRelativePath.IsSafeFilePath(descriptor.MetadataArtifactPath))
        {
            throw new ArgumentException("Host adapter metadata artifact path must be a safe relative path.", parameterName);
        }
    }
}
