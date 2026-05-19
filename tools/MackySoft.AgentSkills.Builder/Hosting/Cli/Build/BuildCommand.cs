using ConsoleAppFramework;
using MackySoft.AgentSkills.Builder.Hosting.Cli.Common.Contracts;
using MackySoft.AgentSkills.Generation;
using MackySoft.AgentSkills.Packaging.Canonical;

namespace MackySoft.AgentSkills.Builder.Hosting.Cli.Build;

/// <summary> Provides the public build command for canonical SKILL package generation. </summary>
internal sealed class BuildCommand
{
    private readonly SkillPackageGenerationService generationService;
    private readonly CanonicalSkillPackageWriter packageWriter;

    /// <summary> Initializes a new instance of the <see cref="BuildCommand" /> class. </summary>
    /// <param name="generationService"> The canonical package generation service. </param>
    /// <param name="packageWriter"> The canonical package writer. </param>
    public BuildCommand (
        SkillPackageGenerationService generationService,
        CanonicalSkillPackageWriter packageWriter)
    {
        this.generationService = generationService ?? throw new ArgumentNullException(nameof(generationService));
        this.packageWriter = packageWriter ?? throw new ArgumentNullException(nameof(packageWriter));
    }

    /// <summary> Generates canonical SKILL package files from source definitions. </summary>
    /// <param name="definitionsRoot"> Required product-owned skill definitions directory.</param>
    /// <param name="generatedRoot"> Required canonical generated skills directory.</param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution.</param>
    /// <returns> The process exit code.</returns>
    [Command(AgentSkillsCommandNames.Build)]
    public async Task<int> BuildAsync (
        string definitionsRoot,
        string generatedRoot,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var packagesResult = await generationService.GenerateAllAsync(definitionsRoot, cancellationToken).ConfigureAwait(false);
        if (!packagesResult.IsSuccess)
        {
            Console.Error.WriteLine(packagesResult.Failure!.Message);
            return 1;
        }

        var writeResult = await packageWriter.WriteAllAsync(
            packagesResult.Value!,
            generatedRoot,
            cleanOutputRoot: true,
            cancellationToken).ConfigureAwait(false);
        if (!writeResult.IsSuccess)
        {
            Console.Error.WriteLine(writeResult.Failure!.Message);
            return 1;
        }

        Console.WriteLine($"Generated canonical skills: {writeResult.Value}");
        return 0;
    }
}
