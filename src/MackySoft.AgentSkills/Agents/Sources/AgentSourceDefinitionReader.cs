using System.Text.Json;
using MackySoft.AgentSkills.Hosts.Registration;
using MackySoft.AgentSkills.Names;
using MackySoft.AgentSkills.Shared;
using MackySoft.AgentSkills.Shared.FileSystem;
using MackySoft.FileSystem;

namespace MackySoft.AgentSkills.Agents.Sources;

/// <summary> Reads agent definitions from fixed <c>agents/&lt;category&gt;/&lt;agent&gt;</c> source directories. </summary>
internal sealed class AgentSourceDefinitionReader
{
    private static readonly string[] ExpectedAgentEntries = ["AGENT.md.template", "agent.json", "hosts"];
    private static readonly string[] ExpectedJsonProperties = ["schemaVersion", "displayName", "description", "skillDependencies"];
    private readonly AgentHostAdapterSet hostAdapters;

    /// <summary> Initializes the reader with the registered host bindings. </summary>
    public AgentSourceDefinitionReader (AgentHostAdapterSet hostAdapters)
    {
        this.hostAdapters = hostAdapters ?? throw new ArgumentNullException(nameof(hostAdapters));
    }

    /// <summary> Reads all agent definitions below the v2 agent namespace root. </summary>
    public async ValueTask<SkillOperationResult<IReadOnlyList<AgentSourceDefinition>>> ReadAllAsync (
        string agentsRoot,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentsRoot);
        cancellationToken.ThrowIfCancellationRequested();

        var rootResult = SourcePathBoundary.ParseRoot(agentsRoot, "Agent definitions root");
        if (!rootResult.IsSuccess)
        {
            return Failure(rootResult.Failure!.Message);
        }

        var root = rootResult.Value!;
        try
        {
            if (!SourcePathBoundary.EntryExists(root))
            {
                return SkillOperationResult<IReadOnlyList<AgentSourceDefinition>>.Success([]);
            }

            var validatedRootResult = SourcePathBoundary.ValidateDirectoryRoot(root, "Agent definitions root");
            if (!validatedRootResult.IsSuccess)
            {
                return Failure(validatedRootResult.Failure!.Message);
            }

            var definitions = new List<AgentSourceDefinition>();
            foreach (var categoryEntry in Directory.GetFileSystemEntries(root.Value).Order(StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var categoryLiteral = Path.GetFileName(categoryEntry);
                if (!AgentCategory.TryCreate(categoryLiteral, out var category))
                {
                    return Failure($"Agent category directory name is invalid: {categoryLiteral}");
                }

                var categoryDirectoryResult = SourcePathBoundary.ResolveDirectory(root, categoryLiteral, "Agent category directory");
                if (!categoryDirectoryResult.IsSuccess)
                {
                    return Failure(categoryDirectoryResult.Failure!.Message);
                }

                var agentEntries = Directory.GetFileSystemEntries(categoryDirectoryResult.Value!.Value).Order(StringComparer.Ordinal).ToArray();
                if (agentEntries.Length == 0)
                {
                    return Failure($"Agent category does not contain any definitions: {categoryLiteral}");
                }

                foreach (var agentEntry in agentEntries)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var agentLiteral = Path.GetFileName(agentEntry);
                    var agentDirectoryResult = SourcePathBoundary.ResolveDirectory(categoryDirectoryResult.Value!, agentLiteral, "Agent definition directory");
                    if (!agentDirectoryResult.IsSuccess)
                    {
                        return Failure(agentDirectoryResult.Failure!.Message);
                    }

                    var definitionResult = await ReadOneAsync(agentDirectoryResult.Value!, category, cancellationToken).ConfigureAwait(false);
                    if (!definitionResult.IsSuccess)
                    {
                        return SkillOperationResult<IReadOnlyList<AgentSourceDefinition>>.FailureResult(
                            definitionResult.Failure!.Code,
                            definitionResult.Failure.Message);
                    }

                    definitions.Add(definitionResult.Value!);
                }
            }

            var duplicate = definitions
                .GroupBy(static definition => definition.Metadata.AgentName)
                .FirstOrDefault(static group => group.Count() > 1);
            return duplicate is null
                ? SkillOperationResult<IReadOnlyList<AgentSourceDefinition>>.Success(Array.AsReadOnly(definitions.ToArray()))
                : Failure($"Agent definitions contain a duplicate agent directory name across categories: {duplicate.Key.Value}");
        }
        catch (Exception exception) when (IsSourceFileSystemException(exception))
        {
            return Failure($"Agent source layout is invalid: {exception.Message}");
        }
    }

    private async ValueTask<SkillOperationResult<AgentSourceDefinition>> ReadOneAsync (
        AbsolutePath agentDirectory,
        AgentCategory category,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var agentLiteral = Path.GetFileName(agentDirectory.Value);
        if (!AgentName.TryCreate(agentLiteral, out var agentName))
        {
            return SingleFailure($"Agent directory name is invalid: {agentLiteral}");
        }

        var entries = Directory.GetFileSystemEntries(agentDirectory.Value)
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (!ExpectedAgentEntries.SequenceEqual(entries))
        {
            return SingleFailure($"Agent definition '{agentName.Value}' must contain only agent.json, AGENT.md.template, and hosts.");
        }

        var metadataPathResult = SourcePathBoundary.ResolveRegularFile(agentDirectory, "agent.json", "Agent metadata file");
        if (!metadataPathResult.IsSuccess)
        {
            return SingleFailure(metadataPathResult.Failure!.Message);
        }

        var instructionsPathResult = SourcePathBoundary.ResolveRegularFile(agentDirectory, "AGENT.md.template", "Agent instructions template");
        if (!instructionsPathResult.IsSuccess)
        {
            return SingleFailure(instructionsPathResult.Failure!.Message);
        }

        AgentSourceMetadata metadata;
        try
        {
            using var stream = File.OpenRead(metadataPathResult.Value!.Value);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return SingleFailure("agent.json root must be an object.");
            }

            var properties = root.EnumerateObject().Select(static property => property.Name).ToArray();
            if (!ExpectedJsonProperties.SequenceEqual(properties))
            {
                return SingleFailure("agent.json must contain only schemaVersion, displayName, description, and skillDependencies in canonical order.");
            }

            var dependencies = root.GetProperty("skillDependencies").EnumerateArray()
                .Select(static item => new SkillName(item.GetString() ?? string.Empty))
                .ToArray();
            metadata = new AgentSourceMetadata(
                root.GetProperty("schemaVersion").GetInt32(),
                category,
                agentName,
                root.GetProperty("displayName").GetString() ?? string.Empty,
                root.GetProperty("description").GetString() ?? string.Empty,
                dependencies);
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidOperationException or FormatException)
        {
            return SingleFailure($"agent.json is invalid for '{agentName.Value}'.");
        }

        var hostBindingsResult = await ReadHostBindingsAsync(agentDirectory, cancellationToken).ConfigureAwait(false);
        if (!hostBindingsResult.IsSuccess)
        {
            return SkillOperationResult<AgentSourceDefinition>.FailureResult(
                hostBindingsResult.Failure!.Code,
                hostBindingsResult.Failure.Message);
        }

        var instructions = SkillTextNormalizer.NormalizeToLf(
            await File.ReadAllTextAsync(instructionsPathResult.Value!.Value, cancellationToken).ConfigureAwait(false));
        try
        {
            return SkillOperationResult<AgentSourceDefinition>.Success(
                new AgentSourceDefinition(metadata, instructions, hostBindingsResult.Value!));
        }
        catch (ArgumentException exception)
        {
            return SingleFailure(exception.Message);
        }
    }

    private async ValueTask<SkillOperationResult<IReadOnlyList<AgentHostBindingSource>>> ReadHostBindingsAsync (
        AbsolutePath agentDirectory,
        CancellationToken cancellationToken)
    {
        var hostsDirectoryResult = SourcePathBoundary.ResolveDirectory(agentDirectory, "hosts", "Agent hosts directory");
        if (!hostsDirectoryResult.IsSuccess)
        {
            return BindingsFailure(hostsDirectoryResult.Failure!.Message);
        }

        var hostEntries = Directory.GetFileSystemEntries(hostsDirectoryResult.Value!.Value).Order(StringComparer.Ordinal).ToArray();
        if (hostEntries.Length == 0)
        {
            return BindingsFailure($"Agent hosts directory does not contain any bindings: {agentDirectory.Value}");
        }

        var bindings = new List<AgentHostBindingSource>(hostEntries.Length);
        foreach (var entryPath in hostEntries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fileName = Path.GetFileName(entryPath);
            if (!fileName.EndsWith(".json", StringComparison.Ordinal)
                || !AgentName.TryCreate(Path.GetFileNameWithoutExtension(fileName), out var hostName)
                || !string.Equals(fileName, $"{hostName.Value}.json", StringComparison.Ordinal))
            {
                return BindingsFailure($"Agent host binding file name is invalid: {fileName}");
            }

            var bindingPathResult = SourcePathBoundary.ResolveRegularFile(
                hostsDirectoryResult.Value!,
                fileName,
                "Agent host binding file");
            if (!bindingPathResult.IsSuccess)
            {
                return BindingsFailure(bindingPathResult.Failure!.Message);
            }

            if (!Vocabulary.TryGetValue(hostName.Value, out AgentHostKind host))
            {
                return SkillOperationResult<IReadOnlyList<AgentHostBindingSource>>.FailureResult(
                    SkillFailureCodes.HostUnsupported,
                    $"Unsupported agent host binding: {hostName.Value}");
            }

            var adapterResult = hostAdapters.GetAdapter(host);
            if (!adapterResult.IsSuccess)
            {
                return SkillOperationResult<IReadOnlyList<AgentHostBindingSource>>.FailureResult(
                    adapterResult.Failure!.Code,
                    adapterResult.Failure.Message);
            }

            var json = SkillTextNormalizer.NormalizeToLf(
                await File.ReadAllTextAsync(bindingPathResult.Value!.Value, cancellationToken).ConfigureAwait(false));
            var validationResult = adapterResult.Value!.ValidateBinding(json);
            if (!validationResult.IsSuccess)
            {
                return BindingsFailure(validationResult.Failure!.Message);
            }

            bindings.Add(new AgentHostBindingSource(host, json));
        }

        return SkillOperationResult<IReadOnlyList<AgentHostBindingSource>>.Success(
            Array.AsReadOnly(bindings.ToArray()));
    }

    private static bool IsSourceFileSystemException (Exception exception)
    {
        return exception is IOException
            or UnauthorizedAccessException
            or NotSupportedException
            or PathTooLongException;
    }

    private static SkillOperationResult<IReadOnlyList<AgentSourceDefinition>> Failure (string message)
    {
        return SkillOperationResult<IReadOnlyList<AgentSourceDefinition>>.FailureResult(SkillFailureCodes.SourceInvalid, message);
    }

    private static SkillOperationResult<AgentSourceDefinition> SingleFailure (string message)
    {
        return SkillOperationResult<AgentSourceDefinition>.FailureResult(SkillFailureCodes.SourceInvalid, message);
    }

    private static SkillOperationResult<IReadOnlyList<AgentHostBindingSource>> BindingsFailure (string message)
    {
        return SkillOperationResult<IReadOnlyList<AgentHostBindingSource>>.FailureResult(SkillFailureCodes.SourceInvalid, message);
    }
}
