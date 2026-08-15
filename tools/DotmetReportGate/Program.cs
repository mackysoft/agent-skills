using System.Text.Json;

return await DotmetReportGate.RunAsync(args);

internal static class DotmetReportGate
{
    public static Task<int> RunAsync (string[] args)
    {
        var options = ParseOptions(args);
        var errors = new List<string>();
        try
        {
            using var provenance = ReadJson(options.Provenance);
            using var rules = ReadJson(options.Rules);
            using var doctor = ReadJson(options.Doctor);
            using var analysis = ReadJson(options.Analysis);

            var provenanceValues = ValidateProvenance(provenance.RootElement, errors);
            ValidateRules(rules.RootElement, errors);
            ValidateDoctor(doctor.RootElement, errors);
            var analysisVerdict = ValidateAnalysis(analysis.RootElement, provenanceValues, errors);
            ValidateCommandExitCodes(provenance.RootElement, analysisVerdict, errors);
            if (errors.Count != 0)
            {
                foreach (var error in errors)
                {
                    Console.Error.WriteLine($"dotmet report gate: {error}");
                }

                return Task.FromResult(2);
            }

            Console.WriteLine($"dotmet report gate: valid analysis verdict '{analysisVerdict}'.");
            return Task.FromResult(analysisVerdict == "fail" ? 1 : 0);
        }
        catch (Exception exception) when (exception is IOException or JsonException or ArgumentException)
        {
            Console.Error.WriteLine($"dotmet report gate: {exception.Message}");
            return Task.FromResult(2);
        }
    }

    private static GateOptions ParseOptions (string[] args)
    {
        string? analysis = null;
        string? rules = null;
        string? doctor = null;
        string? provenance = null;
        for (var index = 0; index < args.Length; index++)
        {
            if (index + 1 >= args.Length)
            {
                throw new ArgumentException($"Missing value for '{args[index]}'.");
            }

            var value = args[++index];
            switch (args[index - 1])
            {
                case "--analysis":
                    analysis = value;
                    break;
                case "--rules":
                    rules = value;
                    break;
                case "--doctor":
                    doctor = value;
                    break;
                case "--provenance":
                    provenance = value;
                    break;
                default:
                    throw new ArgumentException($"Unknown option '{args[index - 1]}'.");
            }
        }

        return new GateOptions(
            RequireOption(analysis, "--analysis"),
            RequireOption(rules, "--rules"),
            RequireOption(doctor, "--doctor"),
            RequireOption(provenance, "--provenance"));
    }

    private static JsonDocument ReadJson (string path)
    {
        if (!File.Exists(path))
        {
            throw new ArgumentException($"Required report does not exist: {path}");
        }

        return JsonDocument.Parse(File.ReadAllText(path));
    }

    private static ProvenanceValues ValidateProvenance (JsonElement root, List<string> errors)
    {
        var run = RequireString(root, "run", "provenance", errors);
        var candidate = RequireString(root, "candidate", "provenance", errors);
        var comparisonBase = RequireString(root, "comparisonBase", "provenance", errors);
        var head = RequireString(root, "head", "provenance", errors);
        RequireString(root, "candidateReference", "provenance", errors);
        RequireString(root, "resolutionMethod", "provenance", errors);
        RequireFullSha(candidate, "provenance candidate", errors);
        RequireFullSha(comparisonBase, "provenance comparisonBase", errors);
        RequireFullSha(head, "provenance head", errors);
        return new ProvenanceValues(run, candidate, comparisonBase, head);
    }

    private static void ValidateRules (JsonElement root, List<string> errors)
    {
        ValidateCommonReport(root, "rules-validation", "rules validation", errors);
        RequireString(root, "verdict", "rules validation", errors, "pass");
        RequireFullCoverage(root, "validationCoverage", "rules validation", errors);
        RequireEmptyArray(root, "issues", "rules validation", errors);
    }

    private static void ValidateDoctor (JsonElement root, List<string> errors)
    {
        ValidateCommonReport(root, "doctor", "doctor", errors);
        RequireString(root, "verdict", "doctor", errors, "pass");
        RequireFullCoverage(root, "diagnosticCoverage", "doctor", errors);
        if (!TryGetArray(root, "diagnostics", "doctor", errors, out var diagnostics))
        {
            return;
        }

        foreach (var diagnostic in diagnostics.EnumerateArray())
        {
            if (TryGetBoolean(diagnostic, "required", "doctor diagnostic", errors, out var required)
                && required)
            {
                RequireString(diagnostic, "diagnosticState", "doctor diagnostic", errors, "ok");
            }
        }
    }

    private static string ValidateAnalysis (JsonElement root, ProvenanceValues provenance, List<string> errors)
    {
        ValidateCommonReport(root, "analysis", "analysis", errors);
        RequireString(root, "status", "analysis", errors, "ok");
        var verdict = RequireString(root, "verdict", "analysis", errors);
        if (verdict is not ("pass" or "warn" or "fail"))
        {
            errors.Add($"analysis verdict must be pass, warn, or fail, but was '{verdict}'.");
        }

        RequireString(root, "analysisCompleteness", "analysis", errors, "full");
        RequireFullCoverage(root, "completeness", "analysis", errors);
        ValidateComparison(root, provenance, errors);
        ValidateExecution(root, errors);
        return verdict;
    }

    private static void ValidateCommonReport (JsonElement root, string expectedKind, string name, List<string> errors)
    {
        RequireNumber(root, "contractVersion", name, errors, 1);
        RequireString(root, "reportKind", name, errors, expectedKind);
        RequireString(root, "engineVersion", name, errors, "0.3.0");
        RequireString(root, "status", name, errors, "ok");
        RequireEmptyArray(root, "partialFailures", name, errors);
        RequireEmptyArray(root, "errors", name, errors);
    }

    private static void ValidateComparison (JsonElement root, ProvenanceValues provenance, List<string> errors)
    {
        if (!TryGetObject(root, "comparison", "analysis", errors, out var comparison))
        {
            return;
        }

        RequireString(comparison, "requestedMode", "analysis comparison", errors, "git");
        RequireString(comparison, "mode", "analysis comparison", errors, "git");
        RequireNull(comparison, "reasonCode", "analysis comparison", errors);
        if (TryGetObject(comparison, "coverage", "analysis comparison", errors, out var coverage))
        {
            RequireString(coverage, "subjectChanges", "analysis comparison coverage", errors, "full");
            RequireString(coverage, "publicSurface", "analysis comparison coverage", errors, "full");
            RequireString(coverage, "ruleAwareEvaluation", "analysis comparison coverage", errors, "full");
        }

        if (TryGetObject(comparison, "source", "analysis comparison", errors, out var source))
        {
            RequireString(source, "reference", "analysis comparison source", errors, provenance.ComparisonBase);
        }

        if (TryGetObject(comparison, "current", "analysis comparison", errors, out var current))
        {
            RequireString(current, "headReference", "analysis comparison current", errors, provenance.Head);
        }

        TryGetObject(comparison, "delta", "analysis comparison", errors, out _);
    }

    private static void ValidateExecution (JsonElement root, List<string> errors)
    {
        if (!TryGetObject(root, "execution", "analysis", errors, out var execution))
        {
            return;
        }

        ValidateRulesAssurance(execution, errors);
        if (TryGetObject(execution, "rulesCoverage", "analysis execution", errors, out var rulesCoverage))
        {
            RequireString(rulesCoverage, "state", "analysis rulesCoverage", errors, "full");
            if (TryGetObject(rulesCoverage, "localInventory", "analysis rulesCoverage", errors, out var localInventory))
            {
                RequireEmptyArray(localInventory, "failures", "analysis localInventory coverage", errors);
            }

            if (TryGetObject(rulesCoverage, "policyCoverage", "analysis rulesCoverage", errors, out var policyCoverage))
            {
                RequireEmptyArray(policyCoverage, "failures", "analysis policy coverage", errors);
            }
        }

        if (!TryGetObject(execution, "filters", "analysis execution", errors, out var filters))
        {
            return;
        }

        TryGetBoolean(filters, "applied", "analysis filters", errors, out var filtersApplied);
        if (filtersApplied)
        {
            errors.Add("analysis filters must not be applied for the report gate.");
        }

        ValidateFilterCounts(filters, "unfiltered", false, errors);
        ValidateFilterCounts(filters, "visible", true, errors);
    }

    private static void ValidateRulesAssurance (JsonElement execution, List<string> errors)
    {
        if (!TryGetObject(execution, "assuranceConfig", "analysis execution", errors, out var assuranceConfig)
            || !TryGetObject(assuranceConfig, "rules", "analysis assuranceConfig", errors, out var rules))
        {
            return;
        }

        RequireString(rules, "changeState", "analysis rules assurance", errors, "unchanged");
        RequireBoolean(rules, "changed", "analysis rules assurance", errors, false);
        var beforeDigest = RequireString(rules, "beforeDigest", "analysis rules assurance", errors);
        var currentDigest = RequireString(rules, "currentDigest", "analysis rules assurance", errors);
        if (!string.IsNullOrEmpty(beforeDigest)
            && !string.IsNullOrEmpty(currentDigest)
            && !string.Equals(beforeDigest, currentDigest, StringComparison.Ordinal))
        {
            errors.Add("analysis rules assurance digests must match.");
        }

        RequireBoolean(rules, "reviewRequired", "analysis rules assurance", errors, false);
        RequireNull(rules, "reasonCode", "analysis rules assurance", errors);
    }

    private static void ValidateCommandExitCodes (JsonElement root, string analysisVerdict, List<string> errors)
    {
        if (!TryGetObject(root, "commandExitCodes", "provenance", errors, out var exitCodes))
        {
            return;
        }

        RequireNumber(exitCodes, "toolRestore", "provenance commandExitCodes", errors, 0);
        RequireNumber(exitCodes, "rulesValidate", "provenance commandExitCodes", errors, 0);
        RequireNumber(exitCodes, "doctor", "provenance commandExitCodes", errors, 0);
        RequireNumber(
            exitCodes,
            "analyze",
            "provenance commandExitCodes",
            errors,
            string.Equals(analysisVerdict, "fail", StringComparison.Ordinal) ? 1 : 0);
    }

    private static void ValidateFilterCounts (JsonElement filters, string propertyName, bool requiresNoOmission, List<string> errors)
    {
        if (!TryGetObject(filters, propertyName, "analysis filters", errors, out var counts))
        {
            return;
        }

        foreach (var name in requiresNoOmission
                     ? new[] { "findingCount", "symbolResultCount", "topReviewTargetCount" }
                     : new[] { "findingCount", "symbolResultCount", "topReviewTargetCount", "failFindingCount" })
        {
            RequireNonNegativeNumber(counts, name, $"analysis filters {propertyName}", errors);
        }

        if (!requiresNoOmission)
        {
            return;
        }

        foreach (var name in new[] { "omittedFindingCount", "omittedSymbolResultCount", "omittedTopReviewTargetCount", "omittedFailFindingCount" })
        {
            RequireNumber(counts, name, "analysis visible filters", errors, 0);
        }
    }

    private static void RequireFullCoverage (JsonElement root, string propertyName, string name, List<string> errors)
    {
        if (!TryGetObject(root, propertyName, name, errors, out var coverage))
        {
            return;
        }

        RequireString(coverage, "overall", $"{name} {propertyName}", errors, "full");
    }

    private static string RequireString (JsonElement element, string propertyName, string name, List<string> errors, string? expectedValue = null)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            errors.Add($"{name} must contain string '{propertyName}'.");
            return string.Empty;
        }

        var value = property.GetString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{name} '{propertyName}' must not be blank.");
        }

        if (expectedValue is not null && !string.Equals(value, expectedValue, StringComparison.Ordinal))
        {
            errors.Add($"{name} '{propertyName}' must be '{expectedValue}', but was '{value}'.");
        }

        return value;
    }

    private static void RequireNumber (JsonElement element, string propertyName, string name, List<string> errors, int expectedValue)
    {
        if (!element.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.Number
            || !property.TryGetInt32(out var value))
        {
            errors.Add($"{name} must contain integer '{propertyName}'.");
            return;
        }

        if (value != expectedValue)
        {
            errors.Add($"{name} '{propertyName}' must be {expectedValue}, but was {value}.");
        }
    }

    private static void RequireNonNegativeNumber (JsonElement element, string propertyName, string name, List<string> errors)
    {
        if (!element.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.Number
            || !property.TryGetInt32(out var value)
            || value < 0)
        {
            errors.Add($"{name} must contain non-negative integer '{propertyName}'.");
        }
    }

    private static void RequireEmptyArray (JsonElement element, string propertyName, string name, List<string> errors)
    {
        if (TryGetArray(element, propertyName, name, errors, out var values) && values.GetArrayLength() != 0)
        {
            errors.Add($"{name} '{propertyName}' must be empty.");
        }
    }

    private static void RequireNull (JsonElement element, string propertyName, string name, List<string> errors)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Null)
        {
            errors.Add($"{name} '{propertyName}' must be null.");
        }
    }

    private static void RequireBoolean (JsonElement element, string propertyName, string name, List<string> errors, bool expectedValue)
    {
        if (!TryGetBoolean(element, propertyName, name, errors, out var value))
        {
            return;
        }

        if (value != expectedValue)
        {
            errors.Add($"{name} '{propertyName}' must be {expectedValue.ToString().ToLowerInvariant()}, but was {value.ToString().ToLowerInvariant()}.");
        }
    }

    private static void RequireFullSha (string value, string name, List<string> errors)
    {
        if (value.Length != 40 || value.Any(character => !Uri.IsHexDigit(character)))
        {
            errors.Add($"{name} must be a full 40-character commit SHA.");
        }
    }

    private static bool TryGetObject (JsonElement element, string propertyName, string name, List<string> errors, out JsonElement value)
    {
        if (element.TryGetProperty(propertyName, out value) && value.ValueKind == JsonValueKind.Object)
        {
            return true;
        }

        errors.Add($"{name} must contain object '{propertyName}'.");
        value = default;
        return false;
    }

    private static bool TryGetArray (JsonElement element, string propertyName, string name, List<string> errors, out JsonElement value)
    {
        if (element.TryGetProperty(propertyName, out value) && value.ValueKind == JsonValueKind.Array)
        {
            return true;
        }

        errors.Add($"{name} must contain array '{propertyName}'.");
        value = default;
        return false;
    }

    private static bool TryGetBoolean (JsonElement element, string propertyName, string name, List<string> errors, out bool value)
    {
        if (element.TryGetProperty(propertyName, out var property) && property.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            value = property.GetBoolean();
            return true;
        }

        errors.Add($"{name} must contain boolean '{propertyName}'.");
        value = false;
        return false;
    }

    private static string RequireOption (string? value, string optionName)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException($"Required option '{optionName}' is missing.")
            : value;
    }

    private sealed record GateOptions (string Analysis, string Rules, string Doctor, string Provenance);

    private sealed record ProvenanceValues (string Run, string Candidate, string ComparisonBase, string Head);
}
