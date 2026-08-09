using MackySoft.AgentSkills.Agents.Installation.Targeting;
using MackySoft.AgentSkills.Hosts.ClaudeCode;
using MackySoft.AgentSkills.Hosts.Codex;
using MackySoft.AgentSkills.Hosts.GitHubCopilot;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Hosts.Registration;

/// <summary>Associates one execution host with its Skill and Agent contracts.</summary>
internal sealed class HostRegistration
{
    private static readonly IReadOnlyList<HostRegistration> BuiltIns = CreateRegistrations();
    private static readonly IReadOnlyDictionary<HostKind, HostRegistration> BuiltInsByHost = BuiltIns
        .ToDictionary(static registration => registration.Host);

    /// <summary>Initializes one complete host registration.</summary>
    internal HostRegistration (
        HostKind host,
        ISkillHostAdapter skillAdapter,
        IAgentHostArtifactAdapter agentArtifactAdapter,
        AgentHostTargetPolicy agentTargetPolicy)
    {
        if (!Vocabulary.IsDefined(host))
        {
            throw new ArgumentOutOfRangeException(nameof(host), host, "Unsupported host value.");
        }

        Host = host;
        SkillAdapter = skillAdapter ?? throw new ArgumentNullException(nameof(skillAdapter));
        AgentArtifactAdapter = agentArtifactAdapter ?? throw new ArgumentNullException(nameof(agentArtifactAdapter));
        AgentTargetPolicy = agentTargetPolicy ?? throw new ArgumentNullException(nameof(agentTargetPolicy));
    }

    /// <summary>Gets the execution host.</summary>
    internal HostKind Host { get; }

    /// <summary>Gets the Skill target and materialization descriptor.</summary>
    internal SkillHostDescriptor Skill => SkillAdapter.Descriptor;

    /// <summary>Gets the Agent target policy.</summary>
    internal AgentHostTargetPolicy AgentTargetPolicy { get; }

    /// <summary>Gets the Skill artifact adapter.</summary>
    internal ISkillHostAdapter SkillAdapter { get; }

    /// <summary>Gets the Agent artifact adapter.</summary>
    internal IAgentHostArtifactAdapter AgentArtifactAdapter { get; }

    /// <summary>Gets all complete built-in host registrations in canonical host order.</summary>
    internal static IReadOnlyList<HostRegistration> Registrations => BuiltIns;

    /// <summary>Gets the complete registration for one execution host.</summary>
    internal static SkillOperationResult<HostRegistration> Get (HostKind host)
    {
        if (!Vocabulary.IsDefined(host))
        {
            return SkillOperationResult<HostRegistration>.FailureResult(
                SkillFailureCodes.HostUnsupported,
                $"Unsupported execution host value: {host}");
        }

        return BuiltInsByHost.TryGetValue(host, out var registration)
            ? SkillOperationResult<HostRegistration>.Success(registration)
            : SkillOperationResult<HostRegistration>.FailureResult(
                SkillFailureCodes.HostUnsupported,
                $"Unsupported execution host: {Vocabulary.GetText(host)}");
    }

    private static IReadOnlyList<HostRegistration> CreateRegistrations ()
    {
        var builtIns = new[]
        {
            CodexHostRegistration.Create(),
            ClaudeCodeHostRegistration.Create(),
            GitHubCopilotHostRegistration.Create(),
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
