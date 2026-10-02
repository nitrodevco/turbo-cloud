using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Turbo.Contracts.Plugins;
using Turbo.Primitives.Commands;
using Turbo.Runtime.AssemblyProcessing;

namespace Turbo.Commands;

public sealed class CommandModule : IHostPluginModule
{
    public string Key => "turbo-commands";

    public void ConfigureServices(IServiceCollection services, HostApplicationBuilder builder)
    {
        services.Configure<CommandConfig>(
            builder.Configuration.GetSection(CommandConfig.SECTION_NAME)
        );
        services
            .AddOptions<CommandConfig>()
            .Validate(
                options =>
                    options.MaxBatchConcurrency > 0
                    && options.FinalizationTimeoutSeconds > 0
                    && options.MaxOutstandingExecutions > 0
                    && options.MaxOutstandingExecutionsPerExecutor > 0
                    && options.ExecutionTimeoutSeconds > 0
                    && options.MaxPendingConfirmations > 0
                    && options.ConfirmationCleanupSeconds > 0
                    && options.ConfirmationSeconds > 0
                    && options.ConfirmAtPlayers > 0
                    && options.MaxSuggestionWindows > 0
                    && options.SuggestionWindowRetentionSeconds > 0
                    && options.SuggestionsPerSecond > 0
                    && options.MaxSuggestions > 0
                    && options.MinPlayerPrefix > 0,
                "Command limits and timeouts must be positive."
            )
            .ValidateOnStart();
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<ICommandRegistryProvider, CommandRegistryProvider>();
        services.AddSingleton<ICommandBatchExecutor, CommandBatchExecutor>();
        services.AddSingleton<IOperatorCommandRunner, OperatorCommandRunner>();
        services.AddSingleton<SuggestionSourceRegistry>();
        services.AddSingleton<CommandArgumentParserRegistry>();
        services.AddSingleton<ICommandTreeService, CommandTreeService>();
        services.AddSingleton<ICommandSuggestionService, CommandSuggestionService>();
        services.AddSingleton<IAssemblyFeatureProcessor, CommandFeatureProcessor>();
    }
}
