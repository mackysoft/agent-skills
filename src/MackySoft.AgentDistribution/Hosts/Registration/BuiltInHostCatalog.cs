using MackySoft.AgentDistribution.Hosts.ClaudeCode;
using MackySoft.AgentDistribution.Hosts.Codex;
using MackySoft.AgentDistribution.Hosts.GitHubCopilot;
using MackySoft.AgentDistribution.Shared;

namespace MackySoft.AgentDistribution.Hosts.Registration;

/// <summary> Provides complete, duplicate-free built-in host registrations. </summary>
internal static class BuiltInHostCatalog
{
    private static readonly IReadOnlyList<HostRegistration> BuiltInRegistrations = CreateRegistrations();
    private static readonly IReadOnlyDictionary<HostKind, HostRegistration> BuiltInRegistrationsByHost = BuiltInRegistrations
        .ToDictionary(static registration => registration.Host);

    /// <summary> Gets all complete built-in host registrations in canonical host order. </summary>
    internal static IReadOnlyList<HostRegistration> Registrations => BuiltInRegistrations;

    /// <summary> Gets the complete registration for one execution host. </summary>
    internal static AgentDistributionOperationResult<HostRegistration> Get (HostKind host)
    {
        if (!Vocabulary.IsDefined(host))
        {
            return AgentDistributionOperationResult<HostRegistration>.FailureResult(
                AgentDistributionFailureCodes.HostUnsupported,
                $"Unsupported execution host value: {host}");
        }

        return BuiltInRegistrationsByHost.TryGetValue(host, out var registration)
            ? AgentDistributionOperationResult<HostRegistration>.Success(registration)
            : AgentDistributionOperationResult<HostRegistration>.FailureResult(
                AgentDistributionFailureCodes.HostUnsupported,
                $"Unsupported execution host: {Vocabulary.GetText(host)}");
    }

    private static IReadOnlyList<HostRegistration> CreateRegistrations ()
    {
        var builtIns = new[]
        {
            CodexHostFactory.Create(),
            ClaudeCodeHostFactory.Create(),
            GitHubCopilotHostFactory.Create(),
        };
        var expectedHosts = Enum.GetValues<HostKind>();
        if (builtIns.Select(static registration => registration.Host).Distinct().Count() != builtIns.Length
            || !expectedHosts.Order().SequenceEqual(builtIns.Select(static registration => registration.Host).Order()))
        {
            throw new InvalidOperationException("Built-in host registrations must contain every HostKind exactly once.");
        }

        return Array.AsReadOnly(builtIns
            .OrderBy(static registration => Vocabulary.GetText(registration.Host), StringComparer.Ordinal)
            .ToArray());
    }
}
