using ConsoleAppFramework;

namespace MackySoft.AgentSkills.ConsoleAppFramework;

internal static class AgentSkillsConsoleAppCommandRegistrar
{
    public static ConsoleApp.ConsoleAppBuilder RegisterAgentSkillsCommands (this ConsoleApp.ConsoleAppBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.Add<AgentSkillsListCommand>("skills");
        app.Add<AgentSkillsExportCommand>("skills");
        app.Add<AgentSkillsInstallCommand>("skills");
        app.Add<AgentSkillsUpdateCommand>("skills");
        app.Add<AgentSkillsUninstallCommand>("skills");
        app.Add<AgentSkillsPruneCommand>("skills");
        app.Add<AgentSkillsDoctorCommand>("skills");
        return app;
    }
}
