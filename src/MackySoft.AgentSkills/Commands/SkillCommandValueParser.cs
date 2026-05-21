using MackySoft.AgentSkills.Distribution;
using MackySoft.AgentSkills.Hosts.Contracts;
using MackySoft.AgentSkills.Hosts.Registration;
using MackySoft.AgentSkills.Installation.Targeting;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Commands;

/// <summary> Parses product-independent SKILL command literals into domain values. </summary>
public static class SkillCommandValueParser
{
    private const string ProjectScopeLiteral = "project";
    private const string UserScopeLiteral = "user";
    private const string DirectoryExportFormatLiteral = "directory";
    private const string ZipExportFormatLiteral = "zip";

    /// <summary> Parses a host literal and resolves it to a registered host descriptor. </summary>
    /// <param name="host"> The raw host literal. Null, empty, and whitespace values fail with <see cref="SkillFailureCodes.InputInvalid" />. </param>
    /// <param name="hostAdapters"> The registered host adapter set used for case-insensitive host lookup. </param>
    /// <returns> The canonical host descriptor, or a structured parsing failure. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="hostAdapters" /> is <see langword="null" />. </exception>
    public static SkillOperationResult<SkillHostDescriptor> ParseHostLiteral (
        string? host,
        SkillHostAdapterSet hostAdapters)
    {
        ArgumentNullException.ThrowIfNull(hostAdapters);

        if (string.IsNullOrWhiteSpace(host))
        {
            return SkillOperationResult<SkillHostDescriptor>.FailureResult(
                SkillFailureCodes.InputInvalid,
                "SKILL host literal must not be empty.");
        }

        var adapterResult = hostAdapters.GetAdapter(host);
        return adapterResult.IsSuccess
            ? SkillOperationResult<SkillHostDescriptor>.Success(adapterResult.Value!.Descriptor)
            : SkillOperationResult<SkillHostDescriptor>.FailureResult(adapterResult.Failure!.Code, adapterResult.Failure.Message);
    }

    /// <summary> Parses an install scope literal. </summary>
    /// <param name="scope"> The raw scope literal. Null, empty, and whitespace values fail with <see cref="SkillFailureCodes.InputInvalid" />. </param>
    /// <returns> The parsed scope kind, or a structured parsing failure. </returns>
    public static SkillOperationResult<SkillScopeKind> ParseScopeLiteral (string? scope)
    {
        if (string.IsNullOrWhiteSpace(scope))
        {
            return SkillOperationResult<SkillScopeKind>.FailureResult(
                SkillFailureCodes.InputInvalid,
                "SKILL scope literal must not be empty.");
        }

        if (string.Equals(scope, ProjectScopeLiteral, StringComparison.OrdinalIgnoreCase))
        {
            return SkillOperationResult<SkillScopeKind>.Success(SkillScopeKind.Project);
        }

        if (string.Equals(scope, UserScopeLiteral, StringComparison.OrdinalIgnoreCase))
        {
            return SkillOperationResult<SkillScopeKind>.Success(SkillScopeKind.User);
        }

        return SkillOperationResult<SkillScopeKind>.FailureResult(
            SkillFailureCodes.InputInvalid,
            $"Unsupported SKILL scope literal: {scope}. Supported scopes: {ProjectScopeLiteral}, {UserScopeLiteral}.");
    }

    /// <summary> Parses an install scope literal and validates that the selected host supports it. </summary>
    /// <param name="scope"> The raw scope literal. Null, empty, and whitespace values fail with <see cref="SkillFailureCodes.InputInvalid" />. </param>
    /// <param name="host"> The selected host descriptor whose project and user scope capabilities are checked. </param>
    /// <returns> The parsed scope kind, or a structured parsing or host-capability failure. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="host" /> is <see langword="null" />. </exception>
    public static SkillOperationResult<SkillScopeKind> ParseScopeLiteral (
        string? scope,
        SkillHostDescriptor host)
    {
        ArgumentNullException.ThrowIfNull(host);

        var scopeResult = ParseScopeLiteral(scope);
        return scopeResult.IsSuccess
            ? ValidateScopeSupport(scopeResult.Value, host)
            : SkillOperationResult<SkillScopeKind>.FailureResult(scopeResult.Failure!.Code, scopeResult.Failure.Message);
    }

    /// <summary> Validates that a parsed install scope is supported by the selected host. </summary>
    /// <param name="scope"> The parsed scope kind. Values outside <see cref="SkillScopeKind.Project" /> and <see cref="SkillScopeKind.User" /> fail with <see cref="SkillFailureCodes.InputInvalid" />. </param>
    /// <param name="host"> The selected host descriptor whose project and user scope capabilities are checked. </param>
    /// <returns> The same scope kind when supported, or a structured input or host-capability failure. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="host" /> is <see langword="null" />. </exception>
    public static SkillOperationResult<SkillScopeKind> ValidateScopeSupport (
        SkillScopeKind scope,
        SkillHostDescriptor host)
    {
        ArgumentNullException.ThrowIfNull(host);

        return scope switch
        {
            SkillScopeKind.Project => host.SupportsProjectScope
                ? SkillOperationResult<SkillScopeKind>.Success(SkillScopeKind.Project)
                : UnsupportedScope(host, scope),
            SkillScopeKind.User => host.SupportsUserScope
                ? SkillOperationResult<SkillScopeKind>.Success(SkillScopeKind.User)
                : UnsupportedScope(host, scope),
            _ => SkillOperationResult<SkillScopeKind>.FailureResult(
                SkillFailureCodes.InputInvalid,
                $"Unsupported SKILL scope value: {scope}."),
        };
    }

    /// <summary> Parses an export format literal. </summary>
    /// <param name="format"> The raw export format literal. Null, empty, and whitespace values fail with <see cref="SkillFailureCodes.InputInvalid" />. </param>
    /// <returns> The parsed export format, or a structured parsing failure. </returns>
    public static SkillOperationResult<SkillExportFormat> ParseExportFormatLiteral (string? format)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            return SkillOperationResult<SkillExportFormat>.FailureResult(
                SkillFailureCodes.InputInvalid,
                "SKILL export format literal must not be empty.");
        }

        if (string.Equals(format, DirectoryExportFormatLiteral, StringComparison.OrdinalIgnoreCase))
        {
            return SkillOperationResult<SkillExportFormat>.Success(SkillExportFormat.Directory);
        }

        if (string.Equals(format, ZipExportFormatLiteral, StringComparison.OrdinalIgnoreCase))
        {
            return SkillOperationResult<SkillExportFormat>.Success(SkillExportFormat.Zip);
        }

        return SkillOperationResult<SkillExportFormat>.FailureResult(
            SkillFailureCodes.InputInvalid,
            $"Unsupported SKILL export format literal: {format}. Supported formats: {DirectoryExportFormatLiteral}, {ZipExportFormatLiteral}.");
    }

    private static SkillOperationResult<SkillScopeKind> UnsupportedScope (
        SkillHostDescriptor host,
        SkillScopeKind scope)
    {
        return SkillOperationResult<SkillScopeKind>.FailureResult(
            SkillFailureCodes.ScopeUnsupported,
            $"SKILL host '{host.HostKey}' does not support {scope} scope.");
    }
}
