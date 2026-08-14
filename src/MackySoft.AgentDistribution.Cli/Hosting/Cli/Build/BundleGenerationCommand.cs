using ConsoleAppFramework;
using MackySoft.AgentDistribution.Bundles;
using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Cli.Hosting.Cli.Build;

/// <summary> Provides the public command for canonical bundle generation. </summary>
internal sealed class BundleGenerationCommand
{
    private readonly SkillBundleBuildService buildService;
    private readonly AgentDistributionBundleBuildService agentDistributionBuildService;
    private readonly BundleSchemaVersionReader schemaVersionReader;

    /// <summary> Initializes a new instance of the <see cref="BundleGenerationCommand" /> class. </summary>
    /// <param name="buildService"> The source and generated bundle reconciliation service. </param>
    /// <param name="agentDistributionBuildService"> The mixed source and generated bundle reconciliation service. </param>
    /// <param name="schemaVersionReader"> The source bundle schema reader. </param>
    public BundleGenerationCommand (SkillBundleBuildService buildService, AgentDistributionBundleBuildService agentDistributionBuildService, BundleSchemaVersionReader schemaVersionReader)
    {
        this.buildService = buildService ?? throw new ArgumentNullException(nameof(buildService));
        this.agentDistributionBuildService = agentDistributionBuildService ?? throw new ArgumentNullException(nameof(agentDistributionBuildService));
        this.schemaVersionReader = schemaVersionReader ?? throw new ArgumentNullException(nameof(schemaVersionReader));
    }

    /// <summary> Reconciles a canonical runtime bundle from an explicit source root to an explicit output root. </summary>
    /// <param name="source"> The root containing the authored <c>bundle.json</c> and schema-specific source directories. </param>
    /// <param name="output"> The separate canonical generated output root. </param>
    /// <param name="check"> Whether to fail without writing when generated output requires changes. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The process exit code. </returns>
    [Command("build")]
    public Task<int> BuildAsync (
        string source,
        string output,
        bool check = false,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(source, output, check, cancellationToken);
    }

    private async Task<int> ExecuteAsync (
        string source,
        string output,
        bool check,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        AbsolutePath sourceRoot;
        AbsolutePath outputRoot;
        try
        {
            sourceRoot = AbsolutePath.Parse(Path.GetFullPath(source));
            outputRoot = AbsolutePath.Parse(Path.GetFullPath(output));
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or NotSupportedException or PathTooLongException)
        {
            Console.Error.WriteLine($"Bundle source or output root is invalid: {exception.Message}");
            return 1;
        }

        var schemaResult = await schemaVersionReader.ReadAsync(sourceRoot, cancellationToken).ConfigureAwait(false);
        if (!schemaResult.IsSuccess)
        {
            Console.Error.WriteLine(schemaResult.Failure!.Message);
            return 1;
        }
        if (schemaResult.Value == SkillBundleDefinition.CurrentSchemaVersion)
        {
            var result = await buildService.BuildAsync(sourceRoot, outputRoot, check, cancellationToken).ConfigureAwait(false);
            if (!result.IsSuccess)
            {
                Console.Error.WriteLine(result.Failure!.Message);
                return 1;
            }

            Console.WriteLine(result.Value!.Changed ? $"Generated canonical skills: {outputRoot} (bundle version {result.Value.Descriptor.SkillBundleVersion})" : $"Canonical skills are up to date: {outputRoot}");
            return 0;
        }
        if (schemaResult.Value == AgentDistributionBundleDefinition.CurrentSchemaVersion)
        {
            var result = await agentDistributionBuildService.BuildAsync(sourceRoot, outputRoot, check, cancellationToken).ConfigureAwait(false);
            if (!result.IsSuccess)
            {
                Console.Error.WriteLine(result.Failure!.Message);
                return 1;
            }

            Console.WriteLine(result.Value!.Changed ? $"Generated canonical agent assets: {outputRoot} (bundle version {result.Value.Descriptor.BundleVersion})" : $"Canonical agent assets are up to date: {outputRoot}");
            return 0;
        }

        Console.Error.WriteLine($"Unsupported bundle schema version: {schemaResult.Value}.");
        return 1;
    }
}
