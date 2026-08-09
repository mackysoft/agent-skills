using ConsoleAppFramework;
using MackySoft.AgentDistribution.Bundles;
using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Cli.Hosting.Cli.Build;

/// <summary> Provides the public build command for canonical SKILL package generation. </summary>
internal sealed class BuildCommand
{
    private readonly SkillBundleBuildService buildService;
    private readonly AgentDistributionBundleBuildService v2BuildService;
    private readonly BundleSchemaVersionReader schemaVersionReader;

    /// <summary> Initializes a new instance of the <see cref="BuildCommand" /> class. </summary>
    /// <param name="buildService"> The source and generated bundle reconciliation service. </param>
    public BuildCommand (SkillBundleBuildService buildService, AgentDistributionBundleBuildService v2BuildService, BundleSchemaVersionReader schemaVersionReader)
    {
        this.buildService = buildService ?? throw new ArgumentNullException(nameof(buildService));
        this.v2BuildService = v2BuildService ?? throw new ArgumentNullException(nameof(v2BuildService));
        this.schemaVersionReader = schemaVersionReader ?? throw new ArgumentNullException(nameof(schemaVersionReader));
    }

    /// <summary> Reconciles a canonical runtime bundle from a fixed-layout source bundle root. </summary>
    /// <param name="root"> The root containing <c>bundle.json</c>, <c>definitions</c>, and generated output. </param>
    /// <param name="skillBundleVersion"> The exact target bundle version. Omit it to preserve the version authored in bundle.json. </param>
    /// <param name="bundleVersion"> The exact target v2 mixed-bundle version. </param>
    /// <param name="check"> Whether to fail without writing when generated output requires changes. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The process exit code. </returns>
    [Command("build")]
    public async Task<int> BuildAsync (
        string root = "skills",
        int? skillBundleVersion = null,
        int? bundleVersion = null,
        bool check = false,
        CancellationToken cancellationToken = default)
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
            if (bundleVersion is not null)
            {
                Console.Error.WriteLine("--bundle-version is valid only for schemaVersion 2 bundles.");
                return 1;
            }

            var result = await buildService.BuildAsync(root, skillBundleVersion, check, cancellationToken).ConfigureAwait(false);
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
            if (skillBundleVersion is not null)
            {
                Console.Error.WriteLine("--skill-bundle-version is valid only for schemaVersion 1 bundles.");
                return 1;
            }

            var result = await v2BuildService.BuildAsync(root, bundleVersion, check, cancellationToken).ConfigureAwait(false);
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
