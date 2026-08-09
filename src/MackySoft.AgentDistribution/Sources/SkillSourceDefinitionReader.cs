using System.Text.Json;
using MackySoft.AgentDistribution.Dependencies;
using MackySoft.AgentDistribution.Paths;
using MackySoft.AgentDistribution.Shared;
using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Sources;

/// <summary> Reads and validates source SKILL definitions from fixed <c>definitions/&lt;category&gt;/&lt;skill&gt;</c> directories. </summary>
public sealed class SkillSourceDefinitionReader
{
    private static readonly string[] ExpectedJsonProperties =
    [
        "schemaVersion",
        "displayName",
        "description",
        "dependencies",
    ];

    /// <summary> Reads all source definitions under a definitions root. </summary>
    /// <param name="definitionsRoot"> The bundle <c>definitions</c> directory path. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> An immutable snapshot of the source definitions, or validation failure. </returns>
    internal async ValueTask<SkillOperationResult<IReadOnlyList<SkillSourceDefinition>>> ReadAllAsync (
        AbsolutePath definitionsRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definitionsRoot);
        cancellationToken.ThrowIfCancellationRequested();

        if (!AuthoredSourcePathResolver.EntryExists(definitionsRoot))
        {
            return Failure($"SKILL definitions directory does not exist: {definitionsRoot.Value}");
        }

        var rootResult = AuthoredSourcePathResolver.ValidateDirectoryRoot(definitionsRoot, "SKILL definitions root");
        if (!rootResult.IsSuccess)
        {
            return Failure(rootResult.Failure!.Message);
        }

        var definitions = new List<SkillSourceDefinition>();
        foreach (var categoryDirectory in Directory.GetDirectories(rootResult.Value!.Value).Order(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var categoryName = Path.GetFileName(categoryDirectory);
            if (!SkillCategory.TryCreate(categoryName, out var category) || category is null)
            {
                return Failure($"Skill category directory name is invalid: {categoryName}");
            }

            var categoryDirectoryResult = AuthoredSourcePathResolver.ResolveDirectory(rootResult.Value!, RootRelativePath.Parse(categoryName), "SKILL category directory");
            if (!categoryDirectoryResult.IsSuccess)
            {
                return Failure(categoryDirectoryResult.Failure!.Message);
            }

            if (File.Exists(Path.Combine(categoryDirectoryResult.Value!.Value, "skill.json")))
            {
                return Failure($"SKILL definitions must use '<category>/<skill>' directories. A skill.json was found directly under category candidate '{categoryName}'.");
            }

            var skillDirectories = Directory.GetDirectories(categoryDirectoryResult.Value.Value).Order(StringComparer.Ordinal).ToArray();
            if (skillDirectories.Length == 0)
            {
                return Failure($"Skill category does not contain any definitions: {categoryName}");
            }

            foreach (var skillDirectory in skillDirectories)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var skillDirectoryName = Path.GetFileName(skillDirectory);
                if (!RootRelativePath.TryParse(skillDirectoryName, out var skillDirectoryRelativePath, out var pathFailure)
                    || skillDirectoryRelativePath.IsRoot)
                {
                    return Failure($"Skill directory name is invalid: {skillDirectoryName}. {pathFailure.Message}");
                }

                var skillDirectoryResult = AuthoredSourcePathResolver.ResolveDirectory(
                    categoryDirectoryResult.Value,
                    skillDirectoryRelativePath,
                    "SKILL definition directory");
                if (!skillDirectoryResult.IsSuccess)
                {
                    return Failure(skillDirectoryResult.Failure!.Message);
                }

                var result = await ReadOneCoreAsync(skillDirectoryResult.Value!, category, cancellationToken).ConfigureAwait(false);
                if (!result.IsSuccess)
                {
                    return Failure(result.Failure!.Message);
                }

                definitions.Add(result.Value!);
            }
        }

        var duplicateSkillName = definitions
            .GroupBy(static definition => definition.Metadata.SkillName)
            .FirstOrDefault(static group => group.Count() > 1);
        if (duplicateSkillName is not null)
        {
            return Failure($"SKILL definitions contain a duplicate skill directory name across categories: {duplicateSkillName.Key.Value}");
        }

        var dependencyResult = ValidateDefinitionDependencies(definitions);
        if (!dependencyResult.IsSuccess)
        {
            return Failure(dependencyResult.Failure!.Message);
        }

        return SkillOperationResult<IReadOnlyList<SkillSourceDefinition>>.Success(
            Array.AsReadOnly(definitions.ToArray()));
    }

    /// <summary> Reads one source definition directory. </summary>
    /// <param name="skillDirectory"> The source skill directory whose parent and own directory names define its category and skill name. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The source definition or validation failure. </returns>
    internal async ValueTask<SkillOperationResult<SkillSourceDefinition>> ReadOneAsync (
        AbsolutePath skillDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(skillDirectory);
        cancellationToken.ThrowIfCancellationRequested();

        var skillDirectoryResult = AuthoredSourcePathResolver.ValidateDirectoryRoot(skillDirectory, "SKILL definition directory");
        if (!skillDirectoryResult.IsSuccess)
        {
            return SkillOperationResult<SkillSourceDefinition>.FailureResult(SkillFailureCodes.SourceInvalid, skillDirectoryResult.Failure!.Message);
        }

        var categoryDirectory = Path.GetDirectoryName(skillDirectoryResult.Value!.Value);
        var categoryName = categoryDirectory is null ? string.Empty : Path.GetFileName(categoryDirectory);
        if (!SkillCategory.TryCreate(categoryName, out var category) || category is null)
        {
            return SkillOperationResult<SkillSourceDefinition>.FailureResult(
                SkillFailureCodes.SourceInvalid,
                $"Skill category directory name is invalid: {categoryName}");
        }

        return await ReadOneCoreAsync(skillDirectoryResult.Value!, category, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<SkillOperationResult<SkillSourceDefinition>> ReadOneCoreAsync (
        AbsolutePath skillDirectory,
        SkillCategory category,
        CancellationToken cancellationToken)
    {
        var skillNameLiteral = Path.GetFileName(skillDirectory.Value);
        if (!SkillName.TryCreate(skillNameLiteral, out var skillName) || skillName is null)
        {
            return SkillOperationResult<SkillSourceDefinition>.FailureResult(
                SkillFailureCodes.SourceInvalid,
                $"Skill directory name is invalid: {skillNameLiteral}");
        }

        var referencesResult = await ReadReferencesAsync(skillDirectory, cancellationToken).ConfigureAwait(false);
        if (!referencesResult.IsSuccess)
        {
            return SkillOperationResult<SkillSourceDefinition>.FailureResult(
                SkillFailureCodes.SourceInvalid,
                referencesResult.Failure!.Message);
        }

        SkillOperationResult<SkillSourceMetadata> metadataResult;
        try
        {
            metadataResult = await ReadMetadataAsync(
                skillDirectory,
                category,
                skillName,
                referencesResult.Value!.Select(static reference => reference.FileName).ToArray(),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException or ArgumentException)
        {
            return SkillOperationResult<SkillSourceDefinition>.FailureResult(
                SkillFailureCodes.SourceInvalid,
                $"skill.json is invalid for '{skillName.Value}'.");
        }

        if (!metadataResult.IsSuccess)
        {
            return SkillOperationResult<SkillSourceDefinition>.FailureResult(SkillFailureCodes.SourceInvalid, metadataResult.Failure!.Message);
        }

        var templatePathResult = AuthoredSourcePathResolver.ResolveRegularFile(skillDirectory, RootRelativePath.Parse("SKILL.md.template"), "SKILL instructions template");
        if (!templatePathResult.IsSuccess)
        {
            return SkillOperationResult<SkillSourceDefinition>.FailureResult(
                SkillFailureCodes.SourceInvalid,
                AuthoredSourcePathResolver.EntryExists(ContainedPath.Create(skillDirectory, RootRelativePath.Parse("SKILL.md.template")).Target)
                    ? templatePathResult.Failure!.Message
                    : $"SKILL.md.template is missing for '{skillName.Value}'.");
        }

        var skillTemplate = SkillTextNormalizer.NormalizeToLf(await File.ReadAllTextAsync(templatePathResult.Value!.Value, cancellationToken).ConfigureAwait(false));

        try
        {
            return SkillOperationResult<SkillSourceDefinition>.Success(
                new SkillSourceDefinition(metadataResult.Value!, skillTemplate, referencesResult.Value!));
        }
        catch (ArgumentException ex)
        {
            return SkillOperationResult<SkillSourceDefinition>.FailureResult(SkillFailureCodes.SourceInvalid, $"Source definition is invalid for '{skillName.Value}': {ex.Message}");
        }
    }

    private static async ValueTask<SkillOperationResult<SkillSourceMetadata>> ReadMetadataAsync (
        AbsolutePath skillDirectory,
        SkillCategory category,
        SkillName skillName,
        IReadOnlyList<string> references,
        CancellationToken cancellationToken)
    {
        var metadataPathResult = AuthoredSourcePathResolver.ResolveRegularFile(skillDirectory, RootRelativePath.Parse("skill.json"), "SKILL metadata file");
        if (!metadataPathResult.IsSuccess)
        {
            return SkillOperationResult<SkillSourceMetadata>.FailureResult(
                SkillFailureCodes.SourceInvalid,
                AuthoredSourcePathResolver.EntryExists(ContainedPath.Create(skillDirectory, RootRelativePath.Parse("skill.json")).Target)
                    ? metadataPathResult.Failure!.Message
                    : $"skill.json is missing for '{skillName.Value}'.");
        }

        using var stream = File.OpenRead(metadataPathResult.Value!.Value);
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
                "skill.json must contain only schemaVersion, displayName, description, and dependencies in canonical order.");
        }

        var schemaVersion = root.GetProperty("schemaVersion").GetInt32();
        var displayName = root.GetProperty("displayName").GetString() ?? string.Empty;
        var description = root.GetProperty("description").GetString() ?? string.Empty;

        var dependenciesResult = ReadDependencies(root, skillName);
        if (!dependenciesResult.IsSuccess)
        {
            return SkillOperationResult<SkillSourceMetadata>.FailureResult(SkillFailureCodes.SourceInvalid, dependenciesResult.Failure!.Message);
        }

        return SkillOperationResult<SkillSourceMetadata>.Success(new SkillSourceMetadata(
            schemaVersion,
            category,
            skillName,
            displayName,
            description,
            dependenciesResult.Value!,
            references));
    }

    private static async ValueTask<SkillOperationResult<IReadOnlyList<SkillSourceReference>>> ReadReferencesAsync (
        AbsolutePath skillDirectory,
        CancellationToken cancellationToken)
    {
        var referencesRoot = ContainedPath.Create(skillDirectory, RootRelativePath.Parse("references")).Target;
        if (!AuthoredSourcePathResolver.EntryExists(referencesRoot))
        {
            return SkillOperationResult<IReadOnlyList<SkillSourceReference>>.Success([]);
        }

        var referencesRootResult = AuthoredSourcePathResolver.ResolveDirectory(skillDirectory, RootRelativePath.Parse("references"), "SKILL references directory");
        if (!referencesRootResult.IsSuccess)
        {
            return SkillOperationResult<IReadOnlyList<SkillSourceReference>>.FailureResult(
                SkillFailureCodes.SourceInvalid,
                referencesRootResult.Failure!.Message);
        }

        var references = new List<SkillSourceReference>();
        foreach (var entryPath in Directory.GetFileSystemEntries(referencesRootResult.Value!.Value).Order(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var entryPathResult = AuthoredSourcePathResolver.ResolveRegularFile(
                referencesRootResult.Value!,
                RootRelativePath.Parse(Path.GetFileName(entryPath)),
                "SKILL reference template");
            if (!entryPathResult.IsSuccess)
            {
                return SkillOperationResult<IReadOnlyList<SkillSourceReference>>.FailureResult(
                    SkillFailureCodes.SourceInvalid,
                    entryPathResult.Failure!.Message);
            }

            var templateFileName = Path.GetFileName(entryPathResult.Value!.Value);
            const string TemplateExtension = ".template";
            if (!templateFileName.EndsWith(TemplateExtension, StringComparison.Ordinal))
            {
                return SkillOperationResult<IReadOnlyList<SkillSourceReference>>.FailureResult(
                    SkillFailureCodes.SourceInvalid,
                    $"Reference file must use the '.md.template' extension: {templateFileName}");
            }

            var referenceFileName = templateFileName[..^TemplateExtension.Length];
            if (!SkillSourceReference.IsValidFileName(referenceFileName))
            {
                return SkillOperationResult<IReadOnlyList<SkillSourceReference>>.FailureResult(
                    SkillFailureCodes.SourceInvalid,
                    $"Reference template file name is invalid: {templateFileName}");
            }

            var template = SkillTextNormalizer.NormalizeToLf(
                await File.ReadAllTextAsync(entryPathResult.Value!.Value, cancellationToken).ConfigureAwait(false));
            references.Add(new SkillSourceReference(referenceFileName, template));
        }

        return SkillOperationResult<IReadOnlyList<SkillSourceReference>>.Success(
            Array.AsReadOnly(references.ToArray()));
    }

    private static SkillOperationResult<IReadOnlyList<SkillName>> ReadDependencies (
        JsonElement root,
        SkillName skillName)
    {
        var dependencies = root.GetProperty("dependencies").EnumerateArray()
            .Select(static element => element.GetString() ?? string.Empty)
            .ToArray();

        var normalizedDependencies = new List<SkillName>(dependencies.Length);
        foreach (var dependency in dependencies)
        {
            if (!SkillName.TryCreate(dependency, out var dependencyName))
            {
                return SkillOperationResult<IReadOnlyList<SkillName>>.FailureResult(
                    SkillFailureCodes.SourceInvalid,
                    $"skill.json dependency is unsafe for '{skillName.Value}': {dependency}");
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
            static definition => definition.Metadata.Dependencies);

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
