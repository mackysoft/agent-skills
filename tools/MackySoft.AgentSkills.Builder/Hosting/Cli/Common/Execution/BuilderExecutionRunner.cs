using ConsoleAppFramework;
using MackySoft.AgentSkills.Builder.Hosting.Cli.Common.Startup;
using MackySoft.AgentSkills.Builder.Hosting.Composition.Common;
using Microsoft.Extensions.DependencyInjection;

namespace MackySoft.AgentSkills.Builder.Hosting.Cli.Common.Execution;

/// <summary> Runs the public builder CLI command pipeline. </summary>
internal sealed class BuilderExecutionRunner
{
    /// <summary> Executes one public CLI invocation. </summary>
    /// <param name="args"> The command-line arguments passed to the process.</param>
    /// <param name="cancellationToken"> The cancellation token propagated by the hosting environment.</param>
    /// <returns> The process exit code determined by command execution.</returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="args" /> is <see langword="null" />. </exception>
    public async Task<int> RunAsync (
        string[] args,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);

        cancellationToken.ThrowIfCancellationRequested();

        using var serviceProvider = CreateServiceProvider();
        var app = AgentSkillsCommandCatalog.RegisterCommands(ConsoleApp.Create());
        var previousServiceProvider = ConsoleApp.ServiceProvider;
        ConsoleApp.ServiceProvider = serviceProvider;

        try
        {
            await app.RunAsync(args, disposeServiceProvider: false).ConfigureAwait(false);
        }
        finally
        {
            ConsoleApp.ServiceProvider = previousServiceProvider;
        }

        return Environment.ExitCode;
    }

    private static ServiceProvider CreateServiceProvider ()
    {
        var services = new ServiceCollection();
        services.AddAgentSkillsBuilderServices();
        return services.BuildServiceProvider();
    }
}
