using MackySoft.AgentDistribution.Agents.Doctor;
using MackySoft.AgentDistribution.Agents.Installation.Services;
using MackySoft.AgentDistribution.Agents.Installation.State;
using MackySoft.AgentDistribution.Agents.Installation.Targeting;
using MackySoft.AgentDistribution.Bundles;
using MackySoft.AgentDistribution.Digests;
using MackySoft.AgentDistribution.Distribution;
using MackySoft.AgentDistribution.Doctor;
using MackySoft.AgentDistribution.Hosting.Commands;
using MackySoft.AgentDistribution.Hosting.Configuration;
using MackySoft.AgentDistribution.Hosting.Reporting;
using MackySoft.AgentDistribution.Installation.Contracts;
using MackySoft.AgentDistribution.Installation.Diffing;
using MackySoft.AgentDistribution.Installation.Inventory;
using MackySoft.AgentDistribution.Installation.Services;
using MackySoft.AgentDistribution.Installation.State;
using MackySoft.AgentDistribution.Installation.Targeting;
using MackySoft.AgentDistribution.Installation.Transactions;
using MackySoft.AgentDistribution.Installation.Validation;
using MackySoft.AgentDistribution.Materialization;
using MackySoft.AgentDistribution.Skills.Bundles;
using MackySoft.AgentDistribution.Skills.Manifests;
using MackySoft.AgentDistribution.Skills.Packaging.Canonical;
using Microsoft.Extensions.DependencyInjection;

namespace MackySoft.AgentDistribution.Hosting.Composition;

/// <summary> Provides service registration for product CLI Agent Distribution command runtimes. </summary>
public static class AgentDistributionCommandRuntimeServiceCollectionExtensions
{
    /// <summary> Registers the standard Agent Distribution command runtime services. </summary>
    /// <param name="services"> The service collection to update. </param>
    /// <param name="configure"> The product-owned command runtime options. </param>
    /// <returns> The same service collection for call chaining. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="services" /> or <paramref name="configure" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when a required option is missing or invalid. </exception>
    public static IServiceCollection AddAgentDistributionCommandRuntime (
        this IServiceCollection services,
        Action<AgentDistributionCommandRuntimeOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new AgentDistributionCommandRuntimeOptions();
        configure(options);
        var configuration = options.CreateValidatedConfiguration();

        services.AddSingleton(configuration);
        services.AddAgentDistributionPackageServices(configuration);
        services.AddAgentDistributionInstallationServices();
        services.AddSingleton<SkillCommandRunner>();
        services.AddSingleton<AgentCommandRunner>();
        services.AddSingleton<IAgentDistributionCommandResultEmitter, AgentDistributionJsonCommandResultEmitter>();

        return services;
    }

    private static IServiceCollection AddAgentDistributionPackageServices (
        this IServiceCollection services,
        AgentDistributionCommandRuntimeConfiguration configuration)
    {
        services.AddSingleton(_ => new BundledAgentDistributionPackageRootResolver(configuration.PackageBaseDirectory));
        services.AddSingleton<PackageContentDigestCalculator>();
        services.AddSingleton<SkillManifestJsonSerializer>();
        services.AddSingleton<SkillManifestDigestCalculator>();
        services.AddSingleton<SkillManifest.Factory>();
        services.AddSingleton<CanonicalSkillPackage.Factory>();
        services.AddSingleton<CanonicalSkillPackageReader>();
        services.AddSingleton<SkillBundleJsonSerializer>();
        services.AddSingleton<SkillBundleDigestCalculator>();
        services.AddSingleton<CanonicalSkillBundle.Factory>();
        services.AddSingleton<CanonicalSkillBundleReader>();
        services.AddSingleton(_ => CanonicalAgentDistributionBundleReader.CreateDefault());
        services.AddSingleton<SkillPackageProvider>();
        services.AddSingleton<AgentPackageProvider>();
        services.AddSingleton<SkillMaterializationService>();
        services.AddSingleton<SkillExportService>();
        services.AddSingleton<AgentExportService>();

        return services;
    }

    private static IServiceCollection AddAgentDistributionInstallationServices (this IServiceCollection services)
    {
        services.AddSingleton(_ => new SkillUserTargetRootResolver(
            static () => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Environment.GetEnvironmentVariable));
        services.AddSingleton<SkillInstalledManifestReader>();
        services.AddSingleton<SkillInstallTargetResolver>();
        services.AddSingleton<SkillCatalogTargetRootSelector>();
        services.AddSingleton<SkillInstalledContentDigestVerifier>();
        services.AddSingleton<SkillInstalledFileSetVerifier>();
        services.AddSingleton<SkillHostMaterializationInspector>();
        services.AddSingleton<SkillInstalledPackageValidator>();
        services.AddSingleton<SkillInstalledPackageIntegrityVerifier>();
        services.AddSingleton<SkillInstalledTargetStateAnalyzer>();
        services.AddSingleton<ISkillPackageDirectoryOperations, SkillPackageDirectoryOperations>();
        services.AddSingleton<ISkillMaterializedPackageWriter, SkillMaterializedPackageWriter>();
        services.AddSingleton<ISkillInstalledPackageRemover, SkillInstalledPackageRemover>();
        services.AddSingleton<SkillMaterializedPackageDiffBuilder>();
        services.AddSingleton<SkillInstallService>();
        services.AddSingleton<SkillUpdateService>();
        services.AddSingleton<SkillUninstallService>();
        services.AddSingleton<SkillPruneService>();
        services.AddSingleton<SkillInstallationScanner>();
        services.AddSingleton<SkillDoctorService>();
        services.AddAgentDistributionAgentInstallationServices();

        return services;
    }

    private static IServiceCollection AddAgentDistributionAgentInstallationServices (this IServiceCollection services)
    {
        services.AddSingleton(_ => new AgentUserTargetRootResolver(
            static () => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Environment.GetEnvironmentVariable));
        services.AddSingleton<AgentInstallTargetResolver>();
        services.AddSingleton<AgentInstallationStateJsonSerializer>();
        services.AddSingleton<AgentInstallationStateStore>();
        services.AddSingleton<AgentInstallationStatePathResolver>();
        services.AddSingleton<AgentInstalledTargetInspector>();
        services.AddSingleton<IAgentManagedArtifactStore, AgentManagedArtifactStore>();
        services.AddSingleton(static serviceProvider => new AgentInstallService(
            serviceProvider.GetRequiredService<AgentInstallTargetResolver>(),
            serviceProvider.GetRequiredService<AgentInstalledTargetInspector>(),
            serviceProvider.GetRequiredService<PackageContentDigestCalculator>(),
            serviceProvider.GetRequiredService<IAgentManagedArtifactStore>(),
            serviceProvider.GetRequiredService<SkillInstallService>()));
        services.AddSingleton(static serviceProvider => new AgentUpdateService(
            serviceProvider.GetRequiredService<AgentInstallTargetResolver>(),
            serviceProvider.GetRequiredService<AgentInstalledTargetInspector>(),
            serviceProvider.GetRequiredService<PackageContentDigestCalculator>(),
            serviceProvider.GetRequiredService<IAgentManagedArtifactStore>(),
            serviceProvider.GetRequiredService<SkillUpdateService>()));
        services.AddSingleton(static serviceProvider => new AgentUninstallService(
            serviceProvider.GetRequiredService<AgentInstallTargetResolver>(),
            serviceProvider.GetRequiredService<AgentInstalledTargetInspector>(),
            serviceProvider.GetRequiredService<AgentInstallationStatePathResolver>(),
            serviceProvider.GetRequiredService<AgentInstallationStateStore>(),
            serviceProvider.GetRequiredService<IAgentManagedArtifactStore>()));
        services.AddSingleton(static serviceProvider => new AgentPruneService(
            serviceProvider.GetRequiredService<AgentInstallTargetResolver>(),
            serviceProvider.GetRequiredService<AgentInstalledTargetInspector>(),
            serviceProvider.GetRequiredService<AgentInstallationStateStore>(),
            serviceProvider.GetRequiredService<IAgentManagedArtifactStore>()));
        services.AddSingleton<AgentDoctorService>();

        return services;
    }
}
