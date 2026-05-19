using ConsoleAppFramework;
using MackySoft.AgentSkills.Builder.Hosting.Cli.Build;

namespace MackySoft.AgentSkills.Builder.Hosting.Cli.Common.Startup;

/// <summary> Provides the single catalog for public builder CLI registration. </summary>
internal static class AgentSkillsCommandCatalog
{
    /// <summary> Registers all supported builder commands with the application builder. </summary>
    /// <param name="app"> The application builder used to register commands.</param>
    /// <returns> The same <paramref name="app" /> instance for call chaining.</returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="app" /> is <see langword="null" />. </exception>
    public static ConsoleApp.ConsoleAppBuilder RegisterCommands (ConsoleApp.ConsoleAppBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.Add<BuildCommand>();
        return app;
    }
}
