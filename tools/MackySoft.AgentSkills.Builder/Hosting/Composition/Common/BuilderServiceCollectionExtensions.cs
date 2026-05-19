using MackySoft.AgentSkills.Builder.Hosting.Composition.Features;
using Microsoft.Extensions.DependencyInjection;

namespace MackySoft.AgentSkills.Builder.Hosting.Composition.Common;

/// <summary> Provides DI registration for the builder host. </summary>
internal static class BuilderServiceCollectionExtensions
{
    /// <summary> Registers all services required by the builder CLI. </summary>
    /// <param name="services"> The target service collection. </param>
    /// <returns> The updated service collection. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="services" /> is <see langword="null" />. </exception>
    public static IServiceCollection AddAgentSkillsBuilderServices (this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddAgentSkillsBuildFeatureServices();
        return services;
    }
}
