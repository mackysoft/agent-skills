using MackySoft.AgentDistribution.Bundles;
using MackySoft.AgentDistribution.Generation;
using MackySoft.AgentDistribution.Packaging.Canonical;
using MackySoft.AgentDistribution.Sources;
using Microsoft.Extensions.DependencyInjection;

namespace MackySoft.AgentDistribution.Cli.Hosting.Composition.Features;

/// <summary> Provides DI registration for canonical package build commands. </summary>
internal static class BuildServiceCollectionExtensions
{
    /// <summary> Registers services required by the <c>build</c> command. </summary>
    /// <param name="services"> The target service collection. </param>
    /// <returns> The updated service collection. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="services" /> is <see langword="null" />. </exception>
    public static IServiceCollection AddAgentDistributionBuildFeatureServices (this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<SkillBundleDefinitionReader>();
        services.AddSingleton<SkillSourceDefinitionReader>();
        services.AddSingleton<SkillPackageGenerationService>();
        services.AddSingleton<CanonicalSkillPackageWriter>();
        services.AddSingleton<CanonicalSkillBundleWriter>();
        services.AddSingleton<SkillBundleBuildService>();
        services.AddSingleton<BundleSchemaVersionReader>();
        services.AddSingleton(_ => AgentDistributionBundleBuildService.CreateDefault());

        return services;
    }
}
