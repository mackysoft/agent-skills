using MackySoft.AgentSkills.Cli.Hosting.Composition.Features;
using MackySoft.AgentSkills.Hosting.Composition;
using Microsoft.Extensions.DependencyInjection;

namespace MackySoft.AgentSkills.Cli.Hosting.Composition.Common;

/// <summary> Provides DI registration for the CLI host. </summary>
internal static class CliServiceCollectionExtensions
{
    /// <summary> Registers all services required by the agent-skills CLI. </summary>
    /// <param name="services"> The target service collection. </param>
    /// <returns> The updated service collection. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="services" /> is <see langword="null" />. </exception>
    public static IServiceCollection AddAgentSkillsCliServices (this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddAgentSkillsCommandRuntime(options =>
        {
            options.ProductName = "Agent Skills CLI";
            options.PackageBaseDirectory = AppContext.BaseDirectory;
            options.CommandRoot = "agent-skills";
        });
        services.AddAgentSkillsBuildFeatureServices();
        return services;
    }
}
