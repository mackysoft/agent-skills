using MackySoft.AgentDistribution.Distribution;
using MackySoft.AgentDistribution.Installation.Targeting;
using MackySoft.AgentDistribution.Shared;
using MackySoft.AgentDistribution.Shared.Text;

namespace MackySoft.AgentDistribution.Commands;

/// <summary> Parses product-independent SKILL command literals into domain values. </summary>
public static class SkillCommandValueParser
{
    /// <summary> Parses a host literal into its canonical host kind. </summary>
    /// <param name="host"> The raw host literal. Null, empty, and whitespace values fail with <see cref="SkillFailureCodes.InputInvalid" />. </param>
    /// <returns> The canonical host kind, or a structured parsing failure. </returns>
    public static SkillOperationResult<HostKind> ParseHostLiteral (string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return SkillOperationResult<HostKind>.FailureResult(
                SkillFailureCodes.InputInvalid,
                "SKILL host literal must not be empty.");
        }

        if (!VocabularyInputParser.TryParseIgnoreCase(host, out HostKind parsedHost))
        {
            return SkillOperationResult<HostKind>.FailureResult(
                SkillFailureCodes.HostUnsupported,
                $"Unsupported SKILL host: {host}. Supported hosts: {string.Join(", ", Vocabulary.GetTexts<HostKind>())}.");
        }

        return SkillOperationResult<HostKind>.Success(parsedHost);
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

        if (VocabularyInputParser.TryParseIgnoreCase(scope, out SkillScopeKind parsedScope))
        {
            return SkillOperationResult<SkillScopeKind>.Success(parsedScope);
        }

        return SkillOperationResult<SkillScopeKind>.FailureResult(
            SkillFailureCodes.InputInvalid,
            $"Unsupported SKILL scope literal: {scope}. Supported scopes: {string.Join(", ", Vocabulary.GetTexts<SkillScopeKind>())}.");
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

        if (VocabularyInputParser.TryParseIgnoreCase(format, out SkillExportFormat parsedFormat))
        {
            return SkillOperationResult<SkillExportFormat>.Success(parsedFormat);
        }

        return SkillOperationResult<SkillExportFormat>.FailureResult(
            SkillFailureCodes.InputInvalid,
            $"Unsupported SKILL export format literal: {format}. Supported formats: {string.Join(", ", Vocabulary.GetTexts<SkillExportFormat>())}.");
    }

}
