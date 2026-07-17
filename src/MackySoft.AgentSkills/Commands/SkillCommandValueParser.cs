using MackySoft.AgentSkills.Distribution;
using MackySoft.AgentSkills.Hosts.Contracts;
using MackySoft.AgentSkills.Hosts.Registration;
using MackySoft.AgentSkills.Installation.Targeting;
using MackySoft.AgentSkills.Shared;
using MackySoft.AgentSkills.Shared.Text;

namespace MackySoft.AgentSkills.Commands;

/// <summary> Parses product-independent SKILL command literals into domain values. </summary>
public static class SkillCommandValueParser
{
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

        if (!ContractLiteralInputParser.TryParseIgnoreCase(host, out SkillHostKind parsedHost))
        {
            return SkillOperationResult<SkillHostDescriptor>.FailureResult(
                SkillFailureCodes.HostUnsupported,
                $"Unsupported SKILL host: {host}. Supported hosts: {string.Join(", ", ContractLiteralCodec.GetLiterals<SkillHostKind>())}.");
        }

        var adapterResult = hostAdapters.GetAdapter(parsedHost);
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

        if (ContractLiteralInputParser.TryParseIgnoreCase(scope, out SkillScopeKind parsedScope))
        {
            return SkillOperationResult<SkillScopeKind>.Success(parsedScope);
        }

        return SkillOperationResult<SkillScopeKind>.FailureResult(
            SkillFailureCodes.InputInvalid,
            $"Unsupported SKILL scope literal: {scope}. Supported scopes: {string.Join(", ", ContractLiteralCodec.GetLiterals<SkillScopeKind>())}.");
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

        if (ContractLiteralInputParser.TryParseIgnoreCase(format, out SkillExportFormat parsedFormat))
        {
            return SkillOperationResult<SkillExportFormat>.Success(parsedFormat);
        }

        return SkillOperationResult<SkillExportFormat>.FailureResult(
            SkillFailureCodes.InputInvalid,
            $"Unsupported SKILL export format literal: {format}. Supported formats: {string.Join(", ", ContractLiteralCodec.GetLiterals<SkillExportFormat>())}.");
    }

}
