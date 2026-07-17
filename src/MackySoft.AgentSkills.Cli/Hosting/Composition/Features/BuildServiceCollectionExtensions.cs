using MackySoft.AgentSkills.Bundles;
using MackySoft.AgentSkills.Generation;
using MackySoft.AgentSkills.Packaging.Canonical;
using MackySoft.AgentSkills.Sources;
using Microsoft.Extensions.DependencyInjection;

namespace MackySoft.AgentSkills.Cli.Hosting.Composition.Features;

/// <summary> Provides DI registration for canonical package build commands. </summary>
internal static class BuildServiceCollectionExtensions
{
    /// <summary> Registers services required by the <c>build</c> command. </summary>
    /// <param name="services"> The target service collection. </param>
    /// <returns> The updated service collection. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="services" /> is <see langword="null" />. </exception>
    public static IServiceCollection AddAgentSkillsBuildFeatureServices (this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<SkillBundleDefinitionReader>();
        services.AddSingleton<SkillSourceDefinitionReader>();
        services.AddSingleton<SkillPackageGenerationService>();
        services.AddSingleton<CanonicalSkillPackageWriter>();
        services.AddSingleton<CanonicalSkillBundleWriter>();
        services.AddSingleton<SkillBundleBuildService>();

        return services;
    }
}
