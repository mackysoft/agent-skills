using MackySoft.AgentDistribution.Hosting.Configuration;
using MackySoft.AgentDistribution.Installation.Targeting;
using MackySoft.AgentDistribution.Shared;
using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Hosting.Commands;

/// <summary>Resolves raw command path options into Foundation path contracts.</summary>
internal static class CommandPathResolver
{
    public static SkillOperationResult<CommandRepositoryContext> ResolveRepositoryContext (
        SkillScopeKind scope,
        string? repositoryRoot,
        AgentDistributionCommandRuntimeConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (scope == SkillScopeKind.User)
        {
            return string.IsNullOrWhiteSpace(repositoryRoot)
                ? SkillOperationResult<CommandRepositoryContext>.Success(new CommandRepositoryContext(scope, null))
                : SkillOperationResult<CommandRepositoryContext>.FailureResult(
                    SkillFailureCodes.InputInvalid,
                    "Option '--repository-root' is not supported when '--scope user' is used.");
        }

        if (string.IsNullOrWhiteSpace(repositoryRoot))
        {
            var resolvedRoot = configuration.RepositoryRootResolver(AbsolutePath.Parse(Directory.GetCurrentDirectory()));
            return resolvedRoot is null
                ? SkillOperationResult<CommandRepositoryContext>.FailureResult(
                    SkillFailureCodes.InputInvalid,
                    "The configured repository-root resolver returned null.")
                : SkillOperationResult<CommandRepositoryContext>.Success(new CommandRepositoryContext(scope, resolvedRoot));
        }

        var result = ResolveRequired(repositoryRoot, "Option '--repository-root' is required for project scope.");
        return result.IsSuccess
            ? SkillOperationResult<CommandRepositoryContext>.Success(new CommandRepositoryContext(scope, result.Value))
            : SkillOperationResult<CommandRepositoryContext>.FailureResult(result.Failure!.Code, result.Failure.Message);
    }

    public static SkillOperationResult<AbsolutePath> ResolveRequired (
        string? path,
        string missingMessage)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return SkillOperationResult<AbsolutePath>.FailureResult(
                SkillFailureCodes.InputInvalid,
                missingMessage);
        }

        try
        {
            return SkillOperationResult<AbsolutePath>.Success(AbsolutePath.Parse(Path.GetFullPath(path)));
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or NotSupportedException or PathTooLongException)
        {
            return SkillOperationResult<AbsolutePath>.FailureResult(
                SkillFailureCodes.PathUnsafe,
                exception.Message);
        }
    }

    public static SkillOperationResult<AbsolutePath> ResolveTarget (
        string targetRoot,
        AbsolutePath? repositoryRoot,
        string optionName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetRoot);

        if (AbsolutePath.TryParse(targetRoot, out var absoluteTargetRoot, out _))
        {
            return SkillOperationResult<AbsolutePath>.Success(absoluteTargetRoot);
        }

        if (repositoryRoot is null)
        {
            return SkillOperationResult<AbsolutePath>.FailureResult(
                SkillFailureCodes.PathUnsafe,
                $"User-scope {optionName} must be an absolute path.");
        }

        if (!RootRelativePath.TryParse(targetRoot, out var relativeTargetRoot, out var failure))
        {
            return SkillOperationResult<AbsolutePath>.FailureResult(
                SkillFailureCodes.PathUnsafe,
                $"Project-scope {optionName} must be absolute or repository-relative: {failure.Message}");
        }

        return SkillOperationResult<AbsolutePath>.Success(
            ContainedPath.Create(repositoryRoot, relativeTargetRoot).Target);
    }
}
