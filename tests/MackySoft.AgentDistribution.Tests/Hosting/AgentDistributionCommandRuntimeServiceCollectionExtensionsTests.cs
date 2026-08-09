using MackySoft.AgentDistribution.Hosting.Commands;
using MackySoft.AgentDistribution.Hosting.Composition;
using MackySoft.AgentDistribution.Hosting.Configuration;
using MackySoft.AgentDistribution.Hosting.Reporting;
using Microsoft.Extensions.DependencyInjection;

namespace MackySoft.AgentDistribution.Tests.Hosting;

public sealed class AgentDistributionCommandRuntimeServiceCollectionExtensionsTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void AddAgentDistributionCommandRuntime_WhenOptionsAreValid_RegistersCommandRuntime ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-hosting", "valid-registration");
        var services = new ServiceCollection();

        services.AddAgentDistributionCommandRuntime(options =>
        {
            options.ProductName = "Example CLI";
            options.PackageBaseDirectory = AbsolutePath.Parse(scope.FullPath);
        });

        using var provider = services.BuildServiceProvider();
        var configuration = provider.GetRequiredService<AgentDistributionCommandRuntimeConfiguration>();

        Assert.Equal("Example CLI", configuration.ProductName);
        Assert.True(configuration.PackageBaseDirectory.IsSameAs(AbsolutePath.Parse(scope.FullPath)));
        Assert.True(configuration.RepositoryRootResolver(AbsolutePath.Parse(Directory.GetCurrentDirectory())).IsSameAs(AbsolutePath.Parse(Directory.GetCurrentDirectory())));
        Assert.NotNull(provider.GetRequiredService<SkillCommandRunner>());
        Assert.NotNull(provider.GetRequiredService<AgentCommandRunner>());
        Assert.IsType<AgentDistributionJsonCommandResultEmitter>(provider.GetRequiredService<IAgentDistributionCommandResultEmitter>());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void AddAgentDistributionCommandRuntime_AllowsEmitterOverrideAfterRuntimeRegistration ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-hosting", "override-emitter");
        var services = new ServiceCollection();

        services.AddAgentDistributionCommandRuntime(options =>
        {
            options.ProductName = "Example CLI";
            options.PackageBaseDirectory = AbsolutePath.Parse(scope.FullPath);
        });
        services.AddSingleton<IAgentDistributionCommandResultEmitter, TestCommandResultEmitter>();

        using var provider = services.BuildServiceProvider();

        Assert.IsType<TestCommandResultEmitter>(provider.GetRequiredService<IAgentDistributionCommandResultEmitter>());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void AddAgentDistributionCommandRuntime_DoesNotExposeMutableOptionsAsRuntimeState ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-hosting", "immutable-configuration");
        var services = new ServiceCollection();
        AgentDistributionCommandRuntimeOptions? configuredOptions = null;

        services.AddAgentDistributionCommandRuntime(options =>
        {
            options.ProductName = "Example CLI";
            options.PackageBaseDirectory = AbsolutePath.Parse(scope.FullPath);
            configuredOptions = options;
        });

        configuredOptions!.ProductName = string.Empty;
        configuredOptions.PackageBaseDirectory = null;

        using var provider = services.BuildServiceProvider();
        var configuration = provider.GetRequiredService<AgentDistributionCommandRuntimeConfiguration>();

        Assert.Equal("Example CLI", configuration.ProductName);
        Assert.True(configuration.PackageBaseDirectory.IsSameAs(AbsolutePath.Parse(scope.FullPath)));
        Assert.Null(provider.GetService<AgentDistributionCommandRuntimeOptions>());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void AddAgentDistributionCommandRuntime_WhenRepositoryRootResolverIsNull_ThrowsArgumentNullException ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-hosting", "null-root-resolver");
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() =>
        {
            services.AddAgentDistributionCommandRuntime(options =>
            {
                options.ProductName = "Example CLI";
                options.PackageBaseDirectory = AbsolutePath.Parse(scope.FullPath);
                options.RepositoryRootResolver = null!;
            });
        });
    }

    private sealed class TestCommandResultEmitter : IAgentDistributionCommandResultEmitter
    {
        public ValueTask<int> EmitAsync (
            AgentDistributionCommandResult result,
            AgentDistributionCommandOutputOptions options,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(result.ExitCode);
        }
    }
}
