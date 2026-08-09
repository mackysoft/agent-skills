using MackySoft.AgentSkills.Hosting.Commands;
using MackySoft.AgentSkills.Hosting.Composition;
using MackySoft.AgentSkills.Hosting.Configuration;
using MackySoft.AgentSkills.Hosting.Reporting;
using MackySoft.Tests;
using Microsoft.Extensions.DependencyInjection;

namespace MackySoft.AgentSkills.Tests.Hosting;

public sealed class AgentSkillsCommandRuntimeServiceCollectionExtensionsTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void AddAgentSkillsCommandRuntime_WhenOptionsAreValid_RegistersCommandRuntime ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-hosting", "valid-registration");
        var services = new ServiceCollection();

        services.AddAgentSkillsCommandRuntime(options =>
        {
            options.ProductName = "Example CLI";
            options.PackageBaseDirectory = AbsolutePath.Parse(scope.FullPath);
        });

        using var provider = services.BuildServiceProvider();
        var configuration = provider.GetRequiredService<AgentSkillsCommandRuntimeConfiguration>();

        Assert.Equal("Example CLI", configuration.ProductName);
        Assert.True(configuration.PackageBaseDirectory.IsSameAs(AbsolutePath.Parse(scope.FullPath)));
        Assert.True(configuration.RepositoryRootResolver(AbsolutePath.Parse(Directory.GetCurrentDirectory())).IsSameAs(AbsolutePath.Parse(Directory.GetCurrentDirectory())));
        Assert.NotNull(provider.GetRequiredService<SkillCommandRunner>());
        Assert.NotNull(provider.GetRequiredService<AgentCommandRunner>());
        Assert.IsType<AgentSkillsJsonCommandResultEmitter>(provider.GetRequiredService<IAgentSkillsCommandResultEmitter>());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void AddAgentSkillsCommandRuntime_AllowsEmitterOverrideAfterRuntimeRegistration ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-hosting", "override-emitter");
        var services = new ServiceCollection();

        services.AddAgentSkillsCommandRuntime(options =>
        {
            options.ProductName = "Example CLI";
            options.PackageBaseDirectory = AbsolutePath.Parse(scope.FullPath);
        });
        services.AddSingleton<IAgentSkillsCommandResultEmitter, TestCommandResultEmitter>();

        using var provider = services.BuildServiceProvider();

        Assert.IsType<TestCommandResultEmitter>(provider.GetRequiredService<IAgentSkillsCommandResultEmitter>());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void AddAgentSkillsCommandRuntime_DoesNotExposeMutableOptionsAsRuntimeState ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-hosting", "immutable-configuration");
        var services = new ServiceCollection();
        AgentSkillsCommandRuntimeOptions? configuredOptions = null;

        services.AddAgentSkillsCommandRuntime(options =>
        {
            options.ProductName = "Example CLI";
            options.PackageBaseDirectory = AbsolutePath.Parse(scope.FullPath);
            configuredOptions = options;
        });

        configuredOptions!.ProductName = string.Empty;
        configuredOptions.PackageBaseDirectory = null;

        using var provider = services.BuildServiceProvider();
        var configuration = provider.GetRequiredService<AgentSkillsCommandRuntimeConfiguration>();

        Assert.Equal("Example CLI", configuration.ProductName);
        Assert.True(configuration.PackageBaseDirectory.IsSameAs(AbsolutePath.Parse(scope.FullPath)));
        Assert.Null(provider.GetService<AgentSkillsCommandRuntimeOptions>());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void AddAgentSkillsCommandRuntime_WhenRepositoryRootResolverIsNull_ThrowsArgumentNullException ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-hosting", "null-root-resolver");
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() =>
        {
            services.AddAgentSkillsCommandRuntime(options =>
            {
                options.ProductName = "Example CLI";
                options.PackageBaseDirectory = AbsolutePath.Parse(scope.FullPath);
                options.RepositoryRootResolver = null!;
            });
        });
    }

    private sealed class TestCommandResultEmitter : IAgentSkillsCommandResultEmitter
    {
        public ValueTask<int> EmitAsync (
            AgentSkillsCommandResult result,
            AgentSkillsCommandOutputOptions options,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(result.ExitCode);
        }
    }
}
