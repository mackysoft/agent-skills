using MackySoft.AgentSkills.Cli.Hosting.Cli.Common.Execution;

namespace MackySoft.AgentSkills.Cli;

internal static class Program
{
    /// <summary> Delegates execution to the agent-skills CLI runner. </summary>
    /// <param name="args"> The command-line arguments passed to the process. </param>
    /// <returns> The process exit code determined by the selected runner. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="args" /> is <see langword="null" />. </exception>
    private static async Task<int> Main (string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var cliRunner = new CliExecutionRunner();
        return await cliRunner.RunAsync(args).ConfigureAwait(false);
    }
}
