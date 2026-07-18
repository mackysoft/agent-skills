using ConsoleAppFramework;
using MackySoft.AgentSkills.Bundles;
using MackySoft.AgentSkills.Cli.Hosting.Cli.Common.Contracts;

namespace MackySoft.AgentSkills.Cli.Hosting.Cli.Build;

/// <summary> Provides the public build command for canonical SKILL package generation. </summary>
internal sealed class BuildCommand
{
    private readonly SkillBundleBuildService buildService;

    /// <summary> Initializes a new instance of the <see cref="BuildCommand" /> class. </summary>
    /// <param name="buildService"> The source and generated bundle reconciliation service. </param>
    public BuildCommand (SkillBundleBuildService buildService)
    {
        this.buildService = buildService ?? throw new ArgumentNullException(nameof(buildService));
    }

    /// <summary> Reconciles a canonical runtime bundle from a fixed-layout source bundle root. </summary>
    /// <param name="root"> The root containing <c>bundle.json</c>, <c>definitions</c>, and generated output. </param>
    /// <param name="skillBundleVersion">--skill-bundle-version, The exact target bundle version. Omit it to preserve the version authored in bundle.json.</param>
    /// <param name="check"> Whether to fail without writing when generated output requires changes. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The process exit code. </returns>
    [Command(AgentSkillsCommandNames.Build)]
    public async Task<int> BuildAsync (
        string root = "skills",
        int? skillBundleVersion = null,
        bool check = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var result = await buildService.BuildAsync(root, skillBundleVersion, check, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            Console.Error.WriteLine(result.Failure!.Message);
            return 1;
        }

        var generatedRoot = Path.Combine(Path.GetFullPath(root), "generated");
        if (!result.Value!.Changed)
        {
            Console.WriteLine($"Canonical skills are up to date: {generatedRoot}");
            return 0;
        }

        Console.WriteLine($"Generated canonical skills: {generatedRoot} (bundle version {result.Value.Descriptor.SkillBundleVersion})");
        return 0;
    }
}
