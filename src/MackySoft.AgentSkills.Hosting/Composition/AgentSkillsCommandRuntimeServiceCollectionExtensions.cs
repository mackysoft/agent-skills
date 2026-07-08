using MackySoft.AgentSkills.Digests;
using MackySoft.AgentSkills.Distribution;
using MackySoft.AgentSkills.Doctor;
using MackySoft.AgentSkills.Hosting.Commands;
using MackySoft.AgentSkills.Hosting.Configuration;
using MackySoft.AgentSkills.Hosting.Reporting;
using MackySoft.AgentSkills.Hosts.Claude;
using MackySoft.AgentSkills.Hosts.Contracts;
using MackySoft.AgentSkills.Hosts.Copilot;
using MackySoft.AgentSkills.Hosts.OpenAi;
using MackySoft.AgentSkills.Hosts.Registration;
using MackySoft.AgentSkills.Installation.Contracts;
using MackySoft.AgentSkills.Installation.Diffing;
using MackySoft.AgentSkills.Installation.Inventory;
using MackySoft.AgentSkills.Installation.Services;
using MackySoft.AgentSkills.Installation.State;
using MackySoft.AgentSkills.Installation.Targeting;
using MackySoft.AgentSkills.Installation.Transactions;
using MackySoft.AgentSkills.Installation.Validation;
using MackySoft.AgentSkills.Manifests;
using MackySoft.AgentSkills.Materialization;
using MackySoft.AgentSkills.Packaging.Canonical;
using Microsoft.Extensions.DependencyInjection;

namespace MackySoft.AgentSkills.Hosting.Composition;

/// <summary> Provides service registration for product CLI Agent Skills command runtimes. </summary>
public static class AgentSkillsCommandRuntimeServiceCollectionExtensions
{
    /// <summary> Registers the standard Agent Skills command runtime services. </summary>
    /// <param name="services"> The service collection to update. </param>
    /// <param name="configure"> The product-owned command runtime options. </param>
    /// <returns> The same service collection for call chaining. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="services" /> or <paramref name="configure" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when a required option is missing or invalid. </exception>
    public static IServiceCollection AddAgentSkillsCommandRuntime (
        this IServiceCollection services,
        Action<AgentSkillsCommandRuntimeOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new AgentSkillsCommandRuntimeOptions();
        configure(options);
        var validatedOptions = options.CreateValidatedCopy();

        services.AddSingleton(validatedOptions);
        services.AddAgentSkillsHostServices();
        services.AddAgentSkillsPackageServices(validatedOptions);
        services.AddAgentSkillsInstallationServices();
        services.AddSingleton<AgentSkillsCommandRunner>();
        services.AddSingleton<IAgentSkillsCommandResultEmitter, AgentSkillsJsonCommandResultEmitter>();

        return services;
    }

    private static IServiceCollection AddAgentSkillsHostServices (this IServiceCollection services)
    {
        services.AddSingleton<ISkillHostAdapter, ClaudeSkillHostAdapter>();
        services.AddSingleton<ISkillHostAdapter, CopilotSkillHostAdapter>();
        services.AddSingleton<ISkillHostAdapter, OpenAiSkillHostAdapter>();
        services.AddSingleton<SkillHostAdapterSet>();

        return services;
    }

    private static IServiceCollection AddAgentSkillsPackageServices (
        this IServiceCollection services,
        AgentSkillsCommandRuntimeOptions options)
    {
        services.AddSingleton(_ => new BundledSkillPackageRootResolver(options.PackageBaseDirectory));
        services.AddSingleton<SkillDigestCalculator>();
        services.AddSingleton<SkillManifestJsonSerializer>();
        services.AddSingleton<SkillManifestDigestCalculator>();
        services.AddSingleton<SkillManifestValidator>();
        services.AddSingleton<CanonicalSkillPackageReader>();
        services.AddSingleton<SkillPackageProvider>();
        services.AddSingleton<SkillMaterializationService>();
        services.AddSingleton<SkillExportService>();

        return services;
    }

    private static IServiceCollection AddAgentSkillsInstallationServices (this IServiceCollection services)
    {
        services.AddSingleton(_ => new SkillUserTargetRootResolver(
            static () => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Environment.GetEnvironmentVariable));
        services.AddSingleton<SkillInstallTargetResolver>();
        services.AddSingleton<SkillInstalledManifestReader>();
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

        return services;
    }
}
