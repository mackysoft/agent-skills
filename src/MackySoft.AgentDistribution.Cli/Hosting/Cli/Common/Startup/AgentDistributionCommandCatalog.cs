using ConsoleAppFramework;
using MackySoft.AgentDistribution.Cli.Hosting.Cli.Build;
using MackySoft.AgentDistribution.ConsoleAppFramework;

namespace MackySoft.AgentDistribution.Cli.Hosting.Cli.Common.Startup;

/// <summary> Provides the single catalog for public agent-distribution CLI registration. </summary>
internal static class AgentDistributionCommandCatalog
{
    /// <summary> Registers all supported CLI commands with the application builder. </summary>
    /// <param name="app"> The application builder used to register commands.</param>
    /// <returns> The same <paramref name="app" /> instance for call chaining.</returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="app" /> is <see langword="null" />. </exception>
    public static ConsoleApp.ConsoleAppBuilder RegisterCommands (ConsoleApp.ConsoleAppBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.Add<BuildCommand>();
        return app.RegisterAgentDistributionCommands();
    }
}
