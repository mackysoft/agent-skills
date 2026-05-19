using MackySoft.AgentSkills.Builder.Hosting.Cli.Common.Execution;

namespace MackySoft.AgentSkills.Builder;

internal static class Program
{
    /// <summary> Delegates execution to the builder CLI runner. </summary>
    /// <param name="args"> The command-line arguments passed to the process. </param>
    /// <returns> The process exit code determined by the selected runner. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="args" /> is <see langword="null" />. </exception>
    private static async Task<int> Main (string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var cliRunner = new BuilderExecutionRunner();
        return await cliRunner.RunAsync(args).ConfigureAwait(false);
    }
}
