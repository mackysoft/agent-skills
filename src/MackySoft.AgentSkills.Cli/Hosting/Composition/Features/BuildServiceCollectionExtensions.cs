using MackySoft.AgentSkills.Digests;
using MackySoft.AgentSkills.Generation;
using MackySoft.AgentSkills.Hosts.Defaults;
using MackySoft.AgentSkills.Manifests;
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

        services.AddSingleton(_ => DefaultSkillHostAdapters.CreateSet());
        services.AddSingleton<SkillSourceDefinitionReader>();
        services.AddSingleton<SkillDigestCalculator>();
        services.AddSingleton<SkillManifestJsonSerializer>();
        services.AddSingleton<SkillManifestDigestCalculator>();
        services.AddSingleton<SkillPackageGenerationService>();
        services.AddSingleton<CanonicalSkillPackageWriter>();

        return services;
    }
}
