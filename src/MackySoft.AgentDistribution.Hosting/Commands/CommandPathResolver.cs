using MackySoft.AgentDistribution.Hosting.Configuration;
using MackySoft.AgentDistribution.Installation.Targeting;
using MackySoft.AgentDistribution.Shared;
using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Hosting.Commands;

/// <summary>Resolves raw command path options into Foundation path contracts.</summary>
internal static class CommandPathResolver
{
    public static AgentDistributionOperationResult<CommandRepositoryContext> ResolveRepositoryContext (
        SkillScopeKind scope,
        string? repositoryRoot,
        AgentDistributionCommandRuntimeConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (scope == SkillScopeKind.User)
        {
            return string.IsNullOrWhiteSpace(repositoryRoot)
                ? AgentDistributionOperationResult<CommandRepositoryContext>.Success(new CommandRepositoryContext(scope, null))
                : AgentDistributionOperationResult<CommandRepositoryContext>.FailureResult(
                    AgentDistributionFailureCodes.InputInvalid,
                    "Option '--repository-root' is not supported when '--scope user' is used.");
        }

        if (string.IsNullOrWhiteSpace(repositoryRoot))
        {
            var resolvedRoot = configuration.RepositoryRootResolver(AbsolutePath.Parse(Directory.GetCurrentDirectory()));
            return resolvedRoot is null
                ? AgentDistributionOperationResult<CommandRepositoryContext>.FailureResult(
                    AgentDistributionFailureCodes.InputInvalid,
                    "The configured repository-root resolver returned null.")
                : AgentDistributionOperationResult<CommandRepositoryContext>.Success(new CommandRepositoryContext(scope, resolvedRoot));
        }

        var result = ResolveRequired(repositoryRoot, "Option '--repository-root' is required for project scope.");
        return result.IsSuccess
            ? AgentDistributionOperationResult<CommandRepositoryContext>.Success(new CommandRepositoryContext(scope, result.Value))
            : AgentDistributionOperationResult<CommandRepositoryContext>.FailureResult(result.Failure!.Code, result.Failure.Message);
    }

    public static AgentDistributionOperationResult<AbsolutePath> ResolveRequired (
        string? path,
        string missingMessage)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return AgentDistributionOperationResult<AbsolutePath>.FailureResult(
                AgentDistributionFailureCodes.InputInvalid,
                missingMessage);
        }

        try
        {
            return AgentDistributionOperationResult<AbsolutePath>.Success(AbsolutePath.Parse(Path.GetFullPath(path)));
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or NotSupportedException or PathTooLongException)
        {
            return AgentDistributionOperationResult<AbsolutePath>.FailureResult(
                AgentDistributionFailureCodes.PathUnsafe,
                exception.Message);
        }
    }

    public static AgentDistributionOperationResult<AbsolutePath> ResolveTarget (
        string targetRoot,
        AbsolutePath? repositoryRoot,
        string optionName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetRoot);

        if (AbsolutePath.TryParse(targetRoot, out var absoluteTargetRoot, out _))
        {
            return AgentDistributionOperationResult<AbsolutePath>.Success(absoluteTargetRoot);
        }

        if (repositoryRoot is null)
        {
            return AgentDistributionOperationResult<AbsolutePath>.FailureResult(
                AgentDistributionFailureCodes.PathUnsafe,
                $"User-scope {optionName} must be an absolute path.");
        }

        if (!RootRelativePath.TryParse(targetRoot, out var relativeTargetRoot, out var failure))
        {
            return AgentDistributionOperationResult<AbsolutePath>.FailureResult(
                AgentDistributionFailureCodes.PathUnsafe,
                $"Project-scope {optionName} must be absolute or repository-relative: {failure.Message}");
        }

        return AgentDistributionOperationResult<AbsolutePath>.Success(
            ContainedPath.Create(repositoryRoot, relativeTargetRoot).Target);
    }
}
