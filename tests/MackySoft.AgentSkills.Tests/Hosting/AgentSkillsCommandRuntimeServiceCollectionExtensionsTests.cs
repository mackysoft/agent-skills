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
            options.PackageBaseDirectory = scope.FullPath;
        });

        using var provider = services.BuildServiceProvider();
        var configuration = provider.GetRequiredService<AgentSkillsCommandRuntimeConfiguration>();

        Assert.Equal("Example CLI", configuration.ProductName);
        Assert.Equal(Path.GetFullPath(scope.FullPath), configuration.PackageBaseDirectory);
        Assert.Equal("skills", configuration.CommandRoot);
        Assert.Equal(Directory.GetCurrentDirectory(), configuration.RepositoryRootResolver(Directory.GetCurrentDirectory()));
        Assert.All(configuration.GetType().GetProperties(), static property => Assert.Null(property.SetMethod));
        Assert.NotNull(provider.GetRequiredService<AgentSkillsCommandRunner>());
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
            options.PackageBaseDirectory = scope.FullPath;
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
            options.PackageBaseDirectory = scope.FullPath;
            configuredOptions = options;
        });

        configuredOptions!.ProductName = string.Empty;
        configuredOptions.PackageBaseDirectory = string.Empty;

        using var provider = services.BuildServiceProvider();
        var configuration = provider.GetRequiredService<AgentSkillsCommandRuntimeConfiguration>();

        Assert.Equal("Example CLI", configuration.ProductName);
        Assert.Equal(Path.GetFullPath(scope.FullPath), configuration.PackageBaseDirectory);
        Assert.Null(provider.GetService<AgentSkillsCommandRuntimeOptions>());
    }

    [Theory]
    [InlineData("Agent Skills")]
    [InlineData("agent-")]
    [InlineData("agent--skills")]
    [InlineData("tools  skills")]
    [Trait("Size", "Small")]
    public void AddAgentSkillsCommandRuntime_WhenCommandRootIsInvalid_ThrowsArgumentException (string commandRoot)
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-hosting", "invalid-command-root");
        var services = new ServiceCollection();

        var exception = Assert.Throws<ArgumentException>(() =>
        {
            services.AddAgentSkillsCommandRuntime(options =>
            {
                options.ProductName = "Example CLI";
                options.PackageBaseDirectory = scope.FullPath;
                options.CommandRoot = commandRoot;
            });
        });

        Assert.Contains("command root", exception.Message, StringComparison.OrdinalIgnoreCase);
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
                options.PackageBaseDirectory = scope.FullPath;
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
