using MackySoft.AgentDistribution.Cli.Hosting.Composition.Features;
using MackySoft.AgentDistribution.Hosting.Composition;
using MackySoft.FileSystem;
using Microsoft.Extensions.DependencyInjection;

namespace MackySoft.AgentDistribution.Cli.Hosting.Composition.Common;

/// <summary> Provides DI registration for the CLI host. </summary>
internal static class CliServiceCollectionExtensions
{
    /// <summary> Registers all services required by the agent-distribution CLI. </summary>
    /// <param name="services"> The target service collection. </param>
    /// <returns> The updated service collection. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="services" /> is <see langword="null" />. </exception>
    public static IServiceCollection AddAgentDistributionCliServices (this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddAgentDistributionCommandRuntime(options =>
        {
            options.ProductName = "Agent Distribution CLI";
            options.PackageBaseDirectory = AbsolutePath.Parse(AppContext.BaseDirectory);
        });
        services.AddAgentDistributionBuildFeatureServices();
        return services;
    }
}
