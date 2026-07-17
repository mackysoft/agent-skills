using ConsoleAppFramework;
using MackySoft.AgentSkills.Cli.Hosting.Cli.Build;
using MackySoft.AgentSkills.ConsoleAppFramework;

namespace MackySoft.AgentSkills.Cli.Hosting.Cli.Common.Startup;

/// <summary> Provides the single catalog for public agent-skills CLI registration. </summary>
internal static class AgentSkillsCommandCatalog
{
    /// <summary> Registers all supported CLI commands with the application builder. </summary>
    /// <param name="app"> The application builder used to register commands.</param>
    /// <returns> The same <paramref name="app" /> instance for call chaining.</returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="app" /> is <see langword="null" />. </exception>
    public static ConsoleApp.ConsoleAppBuilder RegisterCommands (ConsoleApp.ConsoleAppBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.Add<BuildCommand>();
        app.Add<AgentSkillsListCommand>();
        app.Add<AgentSkillsExportCommand>();
        app.Add<AgentSkillsInstallCommand>();
        app.Add<AgentSkillsUpdateCommand>();
        app.Add<AgentSkillsUninstallCommand>();
        app.Add<AgentSkillsPruneCommand>();
        app.Add<AgentSkillsDoctorCommand>();
        return app;
    }
}
