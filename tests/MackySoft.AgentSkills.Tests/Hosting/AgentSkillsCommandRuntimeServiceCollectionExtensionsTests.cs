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
            options.CatalogId = "com.example.skills";
            options.DefinedTiers = ["basic", "advanced"];
            options.PackageBaseDirectory = scope.FullPath;
        });

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<AgentSkillsCommandRuntimeOptions>();

        Assert.Equal("Example CLI", options.ProductName);
        Assert.Equal("com.example.skills", options.CatalogId);
        Assert.Equal(["basic", "advanced"], options.DefinedTiers);
        Assert.Equal(Path.GetFullPath(scope.FullPath), options.PackageBaseDirectory);
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
            options.CatalogId = "com.example.skills";
            options.DefinedTiers = ["basic", "advanced"];
            options.PackageBaseDirectory = scope.FullPath;
        });
        services.AddSingleton<IAgentSkillsCommandResultEmitter, TestCommandResultEmitter>();

        using var provider = services.BuildServiceProvider();

        Assert.IsType<TestCommandResultEmitter>(provider.GetRequiredService<IAgentSkillsCommandResultEmitter>());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void AddAgentSkillsCommandRuntime_WhenDefinedTierIsInvalid_ThrowsArgumentException ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-hosting", "invalid-tier");
        var services = new ServiceCollection();

        var exception = Assert.Throws<ArgumentException>(() =>
        {
            services.AddAgentSkillsCommandRuntime(options =>
            {
                options.ProductName = "Example CLI";
                options.CatalogId = "com.example.skills";
                options.DefinedTiers = ["Basic"];
                options.PackageBaseDirectory = scope.FullPath;
            });
        });

        Assert.Contains("tier", exception.Message, StringComparison.OrdinalIgnoreCase);
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
