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

    /// <summary> Reconciles a canonical runtime bundle from a fixed-layout source bundle root. </summary>
    /// <param name="root"> The root containing <c>bundle.json</c>, <c>definitions</c>, and generated output. </param>
    /// <param name="check"> Whether to fail without writing when generated output requires changes. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The process exit code. </returns>
    [Command("build")]
    public Task<int> BuildAsync (
        string root = "agent-distribution",
        bool check = false,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(root, check, cancellationToken);
    }

    private async Task<int> ExecuteAsync (
        string root,
        bool check,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        AbsolutePath bundleRoot;
        try
        {
            bundleRoot = AbsolutePath.Parse(Path.GetFullPath(root));
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or NotSupportedException or PathTooLongException)
        {
            Console.Error.WriteLine($"Source bundle root is invalid: {exception.Message}");
            return 1;
        }

        var schemaResult = await schemaVersionReader.ReadAsync(bundleRoot, cancellationToken).ConfigureAwait(false);
        if (!schemaResult.IsSuccess)
        {
            Console.Error.WriteLine(schemaResult.Failure!.Message);
            return 1;
        }
        if (schemaResult.Value == SkillBundleDefinition.CurrentSchemaVersion)
        {
            var result = await buildService.BuildAsync(root, check, cancellationToken).ConfigureAwait(false);
            if (!result.IsSuccess)
            {
                Console.Error.WriteLine(result.Failure!.Message);
                return 1;
            }

            Console.WriteLine(result.Value!.Changed ? $"Generated canonical skills: {Path.Combine(Path.GetFullPath(root), "generated")} (bundle version {result.Value.Descriptor.SkillBundleVersion})" : $"Canonical skills are up to date: {Path.Combine(Path.GetFullPath(root), "generated")}");
            return 0;
        }
        if (schemaResult.Value == AgentDistributionBundleDefinition.CurrentSchemaVersion)
        {
            var result = await agentDistributionBuildService.BuildAsync(root, check, cancellationToken).ConfigureAwait(false);
            if (!result.IsSuccess)
            {
                Console.Error.WriteLine(result.Failure!.Message);
                return 1;
            }

            Console.WriteLine(result.Value!.Changed ? $"Generated canonical agent assets: {Path.Combine(Path.GetFullPath(root), "generated")} (bundle version {result.Value.Descriptor.BundleVersion})" : $"Canonical agent assets are up to date: {Path.Combine(Path.GetFullPath(root), "generated")}");
            return 0;
        }

        Console.Error.WriteLine($"Unsupported bundle schema version: {schemaResult.Value}.");
        return 1;
    }
}
