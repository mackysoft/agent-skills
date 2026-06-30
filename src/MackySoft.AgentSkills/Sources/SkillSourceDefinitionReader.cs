using System.Text.Json;
using MackySoft.AgentSkills.Catalogs;
using MackySoft.AgentSkills.Dependencies;
using MackySoft.AgentSkills.Names;
using MackySoft.AgentSkills.Shared;
using MackySoft.AgentSkills.Tiers;

namespace MackySoft.AgentSkills.Sources;

/// <summary> Reads and validates source SKILL definitions from <c>SkillDefinitions</c>. </summary>
public sealed class SkillSourceDefinitionReader
{
    private static readonly string[] ExpectedJsonProperties =
    [
        "schemaVersion",
        "catalogId",
        "tier",
        "skillName",
        "displayName",
        "description",
        "dependencies",
        "references",
    ];

    /// <summary> Reads all source definitions under a definitions root. </summary>
    /// <param name="definitionsRoot"> The <c>SkillDefinitions</c> directory path. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The source definitions or validation failure. </returns>
    public async ValueTask<SkillOperationResult<IReadOnlyList<SkillSourceDefinition>>> ReadAllAsync (
        string definitionsRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionsRoot);
        cancellationToken.ThrowIfCancellationRequested();

        if (!Directory.Exists(definitionsRoot))
        {
            return Failure($"SkillDefinitions directory does not exist: {definitionsRoot}");
        }

        var rootResult = SkillSourcePathBoundary.ResolveDirectoryUnderRoot(definitionsRoot, definitionsRoot);
        if (!rootResult.IsSuccess)
        {
            return Failure(rootResult.Failure!.Message);
        }

        var definitions = new List<SkillSourceDefinition>();
        foreach (var skillDirectory in Directory.GetDirectories(rootResult.Value!).Order(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var skillDirectoryResult = SkillSourcePathBoundary.ResolveDirectoryUnderRoot(rootResult.Value!, skillDirectory);
            if (!skillDirectoryResult.IsSuccess)
            {
                return Failure(skillDirectoryResult.Failure!.Message);
            }

            var result = await ReadOneCoreAsync(skillDirectoryResult.Value!, cancellationToken).ConfigureAwait(false);
            if (!result.IsSuccess)
            {
                return Failure(result.Failure!.Message);
            }

            definitions.Add(result.Value!);
        }

        var dependencyResult = ValidateDefinitionDependencies(definitions);
        if (!dependencyResult.IsSuccess)
        {
            return Failure(dependencyResult.Failure!.Message);
        }

        return SkillOperationResult<IReadOnlyList<SkillSourceDefinition>>.Success(definitions);
    }

    /// <summary> Reads one source definition directory. </summary>
    /// <param name="skillDirectory"> The source skill directory path. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The source definition or validation failure. </returns>
    public async ValueTask<SkillOperationResult<SkillSourceDefinition>> ReadOneAsync (
        string skillDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skillDirectory);
        cancellationToken.ThrowIfCancellationRequested();

        var skillDirectoryResult = SkillSourcePathBoundary.ResolveDirectoryUnderRoot(skillDirectory, skillDirectory);
        if (!skillDirectoryResult.IsSuccess)
        {
            return SkillOperationResult<SkillSourceDefinition>.FailureResult(SkillFailureCodes.SourceInvalid, skillDirectoryResult.Failure!.Message);
        }

        return await ReadOneCoreAsync(skillDirectoryResult.Value!, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<SkillOperationResult<SkillSourceDefinition>> ReadOneCoreAsync (
        string skillDirectory,
        CancellationToken cancellationToken)
    {
        var skillName = Path.GetFileName(Path.GetFullPath(skillDirectory));
        SkillOperationResult<SkillSourceMetadata> metadataResult;
        try
        {
            metadataResult = await ReadMetadataAsync(skillDirectory, skillName, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            return SkillOperationResult<SkillSourceDefinition>.FailureResult(
                SkillFailureCodes.SourceInvalid,
                $"skill.json is invalid for '{skillName}'.");
        }

        if (!metadataResult.IsSuccess)
        {
            return SkillOperationResult<SkillSourceDefinition>.FailureResult(SkillFailureCodes.SourceInvalid, metadataResult.Failure!.Message);
        }

        var templatePath = Path.Combine(skillDirectory, "SKILL.md.template");
        if (!File.Exists(templatePath))
        {
            return SkillOperationResult<SkillSourceDefinition>.FailureResult(SkillFailureCodes.SourceInvalid, $"SKILL.md.template is missing for '{skillName}'.");
        }

        var templatePathResult = SkillSourcePathBoundary.ResolveFileUnderRoot(skillDirectory, templatePath);
        if (!templatePathResult.IsSuccess)
        {
            return SkillOperationResult<SkillSourceDefinition>.FailureResult(SkillFailureCodes.SourceInvalid, templatePathResult.Failure!.Message);
        }

        var skillTemplate = SkillTextNormalizer.NormalizeToLf(await File.ReadAllTextAsync(templatePathResult.Value!, cancellationToken).ConfigureAwait(false));
        if (skillTemplate.TrimStart().StartsWith("---", StringComparison.Ordinal))
        {
            return SkillOperationResult<SkillSourceDefinition>.FailureResult(SkillFailureCodes.SourceInvalid, $"SKILL.md.template must not contain frontmatter: {skillName}");
        }
        if (skillTemplate.TrimStart().StartsWith("# ", StringComparison.Ordinal))
        {
            return SkillOperationResult<SkillSourceDefinition>.FailureResult(SkillFailureCodes.SourceInvalid, $"SKILL.md.template must not contain a top-level heading: {skillName}");
        }

        var references = new List<SkillSourceReference>();
        foreach (var reference in metadataResult.Value!.References)
        {
            var referenceTemplatePath = Path.Combine(skillDirectory, "references", reference + ".template");
            if (!File.Exists(referenceTemplatePath))
            {
                return SkillOperationResult<SkillSourceDefinition>.FailureResult(
                    SkillFailureCodes.SourceInvalid,
                    $"Reference template '{reference}.template' is missing for '{skillName}'.");
            }

            var referenceTemplatePathResult = SkillSourcePathBoundary.ResolveFileUnderRoot(skillDirectory, referenceTemplatePath);
            if (!referenceTemplatePathResult.IsSuccess)
            {
                return SkillOperationResult<SkillSourceDefinition>.FailureResult(SkillFailureCodes.SourceInvalid, referenceTemplatePathResult.Failure!.Message);
            }

            var referenceTemplate = SkillTextNormalizer.NormalizeToLf(await File.ReadAllTextAsync(referenceTemplatePathResult.Value!, cancellationToken).ConfigureAwait(false));
            references.Add(new SkillSourceReference(reference, referenceTemplate));
        }

        return SkillOperationResult<SkillSourceDefinition>.Success(new SkillSourceDefinition(metadataResult.Value, skillTemplate, references));
    }

    private static async ValueTask<SkillOperationResult<SkillSourceMetadata>> ReadMetadataAsync (
        string skillDirectory,
        string expectedSkillName,
        CancellationToken cancellationToken)
    {
        var metadataPath = Path.Combine(skillDirectory, "skill.json");
        if (!File.Exists(metadataPath))
        {
            return SkillOperationResult<SkillSourceMetadata>.FailureResult(SkillFailureCodes.SourceInvalid, $"skill.json is missing for '{expectedSkillName}'.");
        }

        var metadataPathResult = SkillSourcePathBoundary.ResolveFileUnderRoot(skillDirectory, metadataPath);
        if (!metadataPathResult.IsSuccess)
        {
            return SkillOperationResult<SkillSourceMetadata>.FailureResult(SkillFailureCodes.SourceInvalid, metadataPathResult.Failure!.Message);
        }

        using var stream = File.OpenRead(metadataPathResult.Value!);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
        {
            return SkillOperationResult<SkillSourceMetadata>.FailureResult(SkillFailureCodes.SourceInvalid, "skill.json root must be an object.");
        }

        var propertyNames = root.EnumerateObject().Select(static property => property.Name).ToArray();
        if (!ExpectedJsonProperties.SequenceEqual(propertyNames))
        {
            return SkillOperationResult<SkillSourceMetadata>.FailureResult(
                SkillFailureCodes.SourceInvalid,
                "skill.json must contain only schemaVersion, catalogId, tier, skillName, displayName, description, dependencies, and references in canonical order.");
        }

        var schemaVersion = root.GetProperty("schemaVersion").GetInt32();
        if (schemaVersion != SkillSourceMetadata.CurrentSchemaVersion)
        {
            return SkillOperationResult<SkillSourceMetadata>.FailureResult(SkillFailureCodes.SourceInvalid, $"Unsupported skill.json schemaVersion: {schemaVersion}");
        }

        var skillNameLiteral = root.GetProperty("skillName").GetString() ?? string.Empty;
        if (!string.Equals(skillNameLiteral, expectedSkillName, StringComparison.Ordinal))
        {
            return SkillOperationResult<SkillSourceMetadata>.FailureResult(
                SkillFailureCodes.SourceInvalid,
                $"skill.json skillName '{skillNameLiteral}' must match directory name '{expectedSkillName}'.");
        }

        if (!SkillName.TryCreate(skillNameLiteral, out var skillName))
        {
            return SkillOperationResult<SkillSourceMetadata>.FailureResult(
                SkillFailureCodes.SourceInvalid,
                $"skill.json skillName is unsafe: {skillNameLiteral}");
        }

        var displayName = root.GetProperty("displayName").GetString() ?? string.Empty;
        var description = root.GetProperty("description").GetString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(description) || description.Length > 1024)
        {
            return SkillOperationResult<SkillSourceMetadata>.FailureResult(SkillFailureCodes.SourceInvalid, $"skill.json displayName and description are invalid for '{skillNameLiteral}'.");
        }

        if (!SkillTier.TryCreate(root.GetProperty("tier").GetString(), out var tier))
        {
            return SkillOperationResult<SkillSourceMetadata>.FailureResult(SkillFailureCodes.SourceInvalid, $"skill.json tier is invalid for '{skillNameLiteral}'.");
        }

        if (!SkillCatalogId.TryCreate(root.GetProperty("catalogId").GetString(), out var catalogId))
        {
            return SkillOperationResult<SkillSourceMetadata>.FailureResult(SkillFailureCodes.SourceInvalid, $"skill.json catalogId is invalid for '{skillNameLiteral}'.");
        }

        var references = root.GetProperty("references").EnumerateArray().Select(static element => element.GetString() ?? string.Empty).ToArray();
        if (references.Distinct(StringComparer.Ordinal).Count() != references.Length)
        {
            return SkillOperationResult<SkillSourceMetadata>.FailureResult(SkillFailureCodes.SourceInvalid, $"skill.json references are invalid for '{skillNameLiteral}'.");
        }

        var dependenciesResult = ReadDependencies(root, skillName);
        if (!dependenciesResult.IsSuccess)
        {
            return SkillOperationResult<SkillSourceMetadata>.FailureResult(SkillFailureCodes.SourceInvalid, dependenciesResult.Failure!.Message);
        }

        foreach (var reference in references)
        {
            if (!reference.EndsWith(".md", StringComparison.Ordinal)
                || !SkillRelativePath.IsSafePathSegment(reference))
            {
                return SkillOperationResult<SkillSourceMetadata>.FailureResult(SkillFailureCodes.SourceInvalid, $"Reference path is unsafe for '{skillNameLiteral}': {reference}");
            }
        }

        return SkillOperationResult<SkillSourceMetadata>.Success(new SkillSourceMetadata(
            schemaVersion,
            skillName,
            displayName,
            description,
            dependenciesResult.Value!,
            tier!,
            catalogId!,
            references));
    }

    private static SkillOperationResult<IReadOnlyList<SkillName>> ReadDependencies (
        JsonElement root,
        SkillName skillName)
    {
        var dependencies = root.GetProperty("dependencies").EnumerateArray()
            .Select(static element => element.GetString() ?? string.Empty)
            .ToArray();
        if (dependencies.Distinct(StringComparer.Ordinal).Count() != dependencies.Length)
        {
            return SkillOperationResult<IReadOnlyList<SkillName>>.FailureResult(SkillFailureCodes.SourceInvalid, $"skill.json dependencies are invalid for '{skillName.Value}'.");
        }

        var normalizedDependencies = new List<SkillName>(dependencies.Length);
        foreach (var dependency in dependencies)
        {
            if (!SkillName.TryCreate(dependency, out var dependencyName))
            {
                return SkillOperationResult<IReadOnlyList<SkillName>>.FailureResult(
                    SkillFailureCodes.SourceInvalid,
                    $"skill.json dependency is unsafe for '{skillName.Value}': {dependency}");
            }

            if (string.Equals(skillName.Value, dependencyName.Value, StringComparison.Ordinal))
            {
                return SkillOperationResult<IReadOnlyList<SkillName>>.FailureResult(
                    SkillFailureCodes.SourceInvalid,
                    $"skill.json dependency must not reference itself: {skillName.Value}.");
            }

            normalizedDependencies.Add(dependencyName);
        }

        return SkillOperationResult<IReadOnlyList<SkillName>>.Success(normalizedDependencies
            .OrderBy(static dependency => dependency.Value, StringComparer.Ordinal)
            .ToArray());
    }

    private static SkillOperationResult<bool> ValidateDefinitionDependencies (IReadOnlyList<SkillSourceDefinition> definitions)
    {
        var dependenciesBySkillName = definitions.ToDictionary(
            static definition => definition.Metadata.SkillName,
            static definition => definition.Metadata.Dependencies,
            EqualityComparer<SkillName>.Default);

        return SkillDependencyGraphValidator.Validate(
            dependenciesBySkillName,
            SkillFailureCodes.SourceInvalid,
            "skill.json");
    }

    private static SkillOperationResult<IReadOnlyList<SkillSourceDefinition>> Failure (string message)
    {
        return SkillOperationResult<IReadOnlyList<SkillSourceDefinition>>.FailureResult(SkillFailureCodes.SourceInvalid, message);
    }
}
