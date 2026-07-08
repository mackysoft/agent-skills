using MackySoft.AgentSkills.Distribution;
using MackySoft.AgentSkills.Doctor;
using MackySoft.AgentSkills.Hosts.Registration;
using MackySoft.AgentSkills.Installation.Results;
using MackySoft.AgentSkills.Installation.State;
using MackySoft.AgentSkills.Installation.Targeting;
using MackySoft.AgentSkills.Shared;
using MackySoft.AgentSkills.Shared.Text;

namespace MackySoft.AgentSkills.OperationReports.Literals;

/// <summary> Converts AgentSkills domain values to and from stable product-neutral literals. </summary>
public static class SkillLiteralCodec
{
    private static readonly ActionStatusDefinition<SkillInstallActionKind>[] InstallActionStatusDefinitions =
    [
        new(SkillInstallActionKind.Created, SkillOperationActionStatus.Changed),
        new(SkillInstallActionKind.Updated, SkillOperationActionStatus.Changed),
        new(SkillInstallActionKind.NoOp, SkillOperationActionStatus.NoOp),
        new(SkillInstallActionKind.BlockedManagedOverwrite, SkillOperationActionStatus.Blocked),
        new(SkillInstallActionKind.BlockedLocalModification, SkillOperationActionStatus.Blocked),
        new(SkillInstallActionKind.BlockedUnmanaged, SkillOperationActionStatus.Blocked),
    ];

    private static readonly ActionStatusDefinition<SkillUpdateActionKind>[] UpdateActionStatusDefinitions =
    [
        new(SkillUpdateActionKind.Created, SkillOperationActionStatus.Changed),
        new(SkillUpdateActionKind.Updated, SkillOperationActionStatus.Changed),
        new(SkillUpdateActionKind.NoOp, SkillOperationActionStatus.NoOp),
        new(SkillUpdateActionKind.BlockedLocalModification, SkillOperationActionStatus.Blocked),
        new(SkillUpdateActionKind.BlockedUnmanaged, SkillOperationActionStatus.Blocked),
        new(SkillUpdateActionKind.BlockedVersionAhead, SkillOperationActionStatus.Blocked),
    ];

    private static readonly ActionStatusDefinition<SkillUninstallActionKind>[] UninstallActionStatusDefinitions =
    [
        new(SkillUninstallActionKind.Deleted, SkillOperationActionStatus.Changed),
        new(SkillUninstallActionKind.NoOp, SkillOperationActionStatus.NoOp),
        new(SkillUninstallActionKind.SkippedUnmanaged, SkillOperationActionStatus.Skipped),
        new(SkillUninstallActionKind.BlockedLocalModification, SkillOperationActionStatus.Blocked),
    ];

    private static readonly ActionStatusDefinition<SkillPruneActionKind>[] PruneActionStatusDefinitions =
    [
        new(SkillPruneActionKind.Deleted, SkillOperationActionStatus.Changed),
        new(SkillPruneActionKind.SkippedCurrent, SkillOperationActionStatus.NoOp),
        new(SkillPruneActionKind.SkippedForeignCatalog, SkillOperationActionStatus.Skipped),
        new(SkillPruneActionKind.SkippedUnmanaged, SkillOperationActionStatus.Skipped),
        new(SkillPruneActionKind.BlockedLocalModification, SkillOperationActionStatus.Blocked),
        new(SkillPruneActionKind.BlockedManifestInvalid, SkillOperationActionStatus.Blocked),
        new(SkillPruneActionKind.BlockedNameCollision, SkillOperationActionStatus.Blocked),
        new(SkillPruneActionKind.BlockedHostConflict, SkillOperationActionStatus.Blocked),
    ];

    /// <summary> Normalizes a supported host literal to the canonical host key. </summary>
    /// <param name="host"> The caller-provided host literal. Null, empty, and whitespace values return a host-unsupported failure. </param>
    /// <param name="hostAdapters"> The supported host adapter set used as the canonical host source. </param>
    /// <returns> The canonical host key, or a host-unsupported failure when <paramref name="host" /> is not supported. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="hostAdapters" /> is <see langword="null" />. </exception>
    public static SkillOperationResult<string> NormalizeHost (
        string? host,
        SkillHostAdapterSet hostAdapters)
    {
        ArgumentNullException.ThrowIfNull(hostAdapters);

        var adapterResult = hostAdapters.GetAdapter(host!);
        return adapterResult.IsSuccess
            ? SkillOperationResult<string>.Success(adapterResult.Value!.Descriptor.HostKey)
            : SkillOperationResult<string>.FailureResult(adapterResult.Failure!.Code, adapterResult.Failure.Message);
    }

    /// <summary> Formats an install scope as a stable lower camel literal. </summary>
    /// <param name="scope"> The install scope to format. </param>
    /// <returns> The stable literal for <paramref name="scope" />. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="scope" /> is not a defined <see cref="SkillScopeKind" /> value. </exception>
    public static string FormatScope (SkillScopeKind scope)
    {
        return FormatContractLiteral(scope, nameof(scope), "Unsupported SKILL install scope.");
    }

    /// <summary> Parses a stable install scope literal. </summary>
    /// <param name="literal"> The scope literal to parse. Null, empty, and whitespace values are rejected. </param>
    /// <param name="scope"> The parsed scope when this method returns <see langword="true" />. </param>
    /// <returns> <see langword="true" /> when <paramref name="literal" /> is a supported scope literal; otherwise <see langword="false" />. </returns>
    public static bool TryParseScope (
        string? literal,
        out SkillScopeKind scope)
    {
        return TryParseIgnoreCase(literal, out scope);
    }

    /// <summary> Formats an export format as a stable lower camel literal. </summary>
    /// <param name="format"> The export format to format. </param>
    /// <returns> The stable literal for <paramref name="format" />. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="format" /> is not a defined <see cref="SkillExportFormat" /> value. </exception>
    public static string FormatExportFormat (SkillExportFormat format)
    {
        return FormatContractLiteral(format, nameof(format), "Unsupported SKILL export format.");
    }

    /// <summary> Parses a stable export format literal. </summary>
    /// <param name="literal"> The export format literal to parse. Null, empty, and whitespace values are rejected. </param>
    /// <param name="format"> The parsed export format when this method returns <see langword="true" />. </param>
    /// <returns> <see langword="true" /> when <paramref name="literal" /> is a supported export format literal; otherwise <see langword="false" />. </returns>
    public static bool TryParseExportFormat (
        string? literal,
        out SkillExportFormat format)
    {
        return TryParseIgnoreCase(literal, out format);
    }

    /// <summary> Formats an install action as a stable lower camel literal. </summary>
    /// <param name="actionKind"> The install action kind to format. </param>
    /// <returns> The stable literal for <paramref name="actionKind" />. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="actionKind" /> is not a defined <see cref="SkillInstallActionKind" /> value. </exception>
    public static string FormatInstallAction (SkillInstallActionKind actionKind)
    {
        return FormatContractLiteral(actionKind, nameof(actionKind), "Unsupported SKILL install action kind.");
    }

    /// <summary> Formats an update action as a stable lower camel literal. </summary>
    /// <param name="actionKind"> The update action kind to format. </param>
    /// <returns> The stable literal for <paramref name="actionKind" />. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="actionKind" /> is not a defined <see cref="SkillUpdateActionKind" /> value. </exception>
    public static string FormatUpdateAction (SkillUpdateActionKind actionKind)
    {
        return FormatContractLiteral(actionKind, nameof(actionKind), "Unsupported SKILL update action kind.");
    }

    /// <summary> Formats an uninstall action as a stable lower camel literal. </summary>
    /// <param name="actionKind"> The uninstall action kind to format. </param>
    /// <returns> The stable literal for <paramref name="actionKind" />. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="actionKind" /> is not a defined <see cref="SkillUninstallActionKind" /> value. </exception>
    public static string FormatUninstallAction (SkillUninstallActionKind actionKind)
    {
        return FormatContractLiteral(actionKind, nameof(actionKind), "Unsupported SKILL uninstall action kind.");
    }

    /// <summary> Formats a prune action as a stable lower camel literal. </summary>
    /// <param name="actionKind"> The prune action kind to format. </param>
    /// <returns> The stable literal for <paramref name="actionKind" />. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="actionKind" /> is not a defined <see cref="SkillPruneActionKind" /> value. </exception>
    public static string FormatPruneAction (SkillPruneActionKind actionKind)
    {
        return FormatContractLiteral(actionKind, nameof(actionKind), "Unsupported SKILL prune action kind.");
    }

    /// <summary> Formats a coarse action status as a stable lower camel literal. </summary>
    /// <param name="status"> The operation action status to format. </param>
    /// <returns> The stable literal for <paramref name="status" />. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="status" /> is not a defined <see cref="SkillOperationActionStatus" /> value. </exception>
    internal static string FormatActionStatus (SkillOperationActionStatus status)
    {
        return FormatContractLiteral(status, nameof(status), "Unsupported SKILL operation action status.");
    }

    /// <summary> Formats a blocked reason as a stable lower camel literal. </summary>
    /// <param name="reason"> The blocked reason to format. </param>
    /// <returns> The stable literal for <paramref name="reason" />. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="reason" /> is not a defined <see cref="SkillBlockedReason" /> value. </exception>
    public static string FormatBlockedReason (SkillBlockedReason reason)
    {
        return FormatContractLiteral(reason, nameof(reason), "Unsupported SKILL blocked reason.");
    }

    /// <summary> Formats an action target state kind as a stable lower camel literal. </summary>
    /// <param name="kind"> The target state kind to format. </param>
    /// <returns> The stable literal for <paramref name="kind" />. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="kind" /> is not a defined <see cref="SkillActionTargetStateKind" /> value. </exception>
    public static string FormatTargetStateKind (SkillActionTargetStateKind kind)
    {
        return FormatContractLiteral(kind, nameof(kind), "Unsupported SKILL target state kind.");
    }

    /// <summary> Formats an installed target state kind as a stable lower camel literal. </summary>
    /// <param name="kind"> The target state kind to format. </param>
    /// <returns> The stable literal for <paramref name="kind" />. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="kind" /> is not a defined <see cref="SkillInstalledTargetStateKind" /> value. </exception>
    public static string FormatTargetStateKind (SkillInstalledTargetStateKind kind)
    {
        return FormatContractLiteral(kind, nameof(kind), "Unsupported SKILL target state kind.");
    }

    /// <summary> Formats a diff change kind as a stable lower camel literal. </summary>
    /// <param name="changeKind"> The diff change kind to format. </param>
    /// <returns> The stable literal for <paramref name="changeKind" />. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="changeKind" /> is not a defined <see cref="SkillDiffChangeKind" /> value. </exception>
    public static string FormatDiffChangeKind (SkillDiffChangeKind changeKind)
    {
        return FormatContractLiteral(changeKind, nameof(changeKind), "Unsupported SKILL diff change kind.");
    }

    /// <summary> Formats a doctor severity as a stable lower camel literal. </summary>
    /// <param name="severity"> The doctor severity to format. </param>
    /// <returns> The stable literal for <paramref name="severity" />. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="severity" /> is not a defined <see cref="SkillDoctorSeverity" /> value. </exception>
    public static string FormatDoctorSeverity (SkillDoctorSeverity severity)
    {
        return FormatContractLiteral(severity, nameof(severity), "Unsupported SKILL doctor severity.");
    }

    /// <summary> Formats a failure code as its stable machine-readable value. </summary>
    /// <param name="code"> The failure code to format. Must contain a non-empty value. </param>
    /// <returns> The raw stable failure code value. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="code" /> does not contain a valid value. </exception>
    public static string FormatFailureCode (SkillFailureCode code)
    {
        if (!code.IsValid)
        {
            throw new ArgumentException("Failure code must be valid.", nameof(code));
        }

        return code.Value;
    }

    internal static IReadOnlyList<string> GetInstallActionLiterals ()
    {
        return ContractLiteralCodec.GetLiterals<SkillInstallActionKind>();
    }

    internal static IReadOnlyList<string> GetUpdateActionLiterals ()
    {
        return ContractLiteralCodec.GetLiterals<SkillUpdateActionKind>();
    }

    internal static IReadOnlyList<string> GetUninstallActionLiterals ()
    {
        return ContractLiteralCodec.GetLiterals<SkillUninstallActionKind>();
    }

    internal static IReadOnlyList<string> GetPruneActionLiterals ()
    {
        return ContractLiteralCodec.GetLiterals<SkillPruneActionKind>();
    }

    internal static IReadOnlyList<string> GetActionStatusLiterals ()
    {
        return ContractLiteralCodec.GetLiterals<SkillOperationActionStatus>();
    }

    internal static SkillOperationActionStatus GetInstallActionStatus (SkillInstallActionKind actionKind)
    {
        return GetActionStatus(InstallActionStatusDefinitions, actionKind, nameof(actionKind), "Unsupported SKILL install action kind.");
    }

    internal static SkillOperationActionStatus GetUpdateActionStatus (SkillUpdateActionKind actionKind)
    {
        return GetActionStatus(UpdateActionStatusDefinitions, actionKind, nameof(actionKind), "Unsupported SKILL update action kind.");
    }

    internal static SkillOperationActionStatus GetUninstallActionStatus (SkillUninstallActionKind actionKind)
    {
        return GetActionStatus(UninstallActionStatusDefinitions, actionKind, nameof(actionKind), "Unsupported SKILL uninstall action kind.");
    }

    internal static SkillOperationActionStatus GetPruneActionStatus (SkillPruneActionKind actionKind)
    {
        return GetActionStatus(PruneActionStatusDefinitions, actionKind, nameof(actionKind), "Unsupported SKILL prune action kind.");
    }

    private static string FormatContractLiteral<TEnum> (
        TEnum value,
        string parameterName,
        string message)
        where TEnum : struct, Enum
    {
        return ContractLiteralCodec.TryToValue(value, out var literal)
            ? literal
            : throw new ArgumentOutOfRangeException(parameterName, value, message);
    }

    private static bool TryParseIgnoreCase<TEnum> (
        string? literal,
        out TEnum value)
        where TEnum : struct, Enum
    {
        if (literal is null)
        {
            value = default;
            return false;
        }

        foreach (var candidateLiteral in ContractLiteralCodec.GetLiterals<TEnum>())
        {
            if (string.Equals(literal, candidateLiteral, StringComparison.OrdinalIgnoreCase))
            {
                return ContractLiteralCodec.TryParse(candidateLiteral, out value);
            }
        }

        value = default;
        return false;
    }

    private static SkillOperationActionStatus GetActionStatus<TActionKind> (
        IReadOnlyList<ActionStatusDefinition<TActionKind>> definitions,
        TActionKind actionKind,
        string parameterName,
        string message)
        where TActionKind : struct, Enum
    {
        return TryGetActionStatusDefinition(definitions, actionKind, out var definition)
            ? definition.Status
            : throw new ArgumentOutOfRangeException(parameterName, actionKind, message);
    }

    private static bool TryGetActionStatusDefinition<TActionKind> (
        IReadOnlyList<ActionStatusDefinition<TActionKind>> definitions,
        TActionKind actionKind,
        out ActionStatusDefinition<TActionKind> definition)
        where TActionKind : struct, Enum
    {
        foreach (var candidate in definitions)
        {
            if (EqualityComparer<TActionKind>.Default.Equals(candidate.Kind, actionKind))
            {
                definition = candidate;
                return true;
            }
        }

        definition = default;
        return false;
    }

    private readonly record struct ActionStatusDefinition<TActionKind> (
        TActionKind Kind,
        SkillOperationActionStatus Status)
        where TActionKind : struct, Enum;
}
