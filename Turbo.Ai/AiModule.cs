using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Turbo.Ai.Approvals;
using Turbo.Ai.Configuration;
using Turbo.Ai.Drivers;
using Turbo.Ai.Storage;
using Turbo.Contracts.Approvals;
using Turbo.Contracts.Plugins;
using Turbo.Plugins;
using Turbo.Primitives.Plugins;

namespace Turbo.Ai;

/// <summary>
/// Language models for plugins (<c>docs/ai-sdk.md</c>): the providers configured under
/// <c>Turbo:Ai</c>, the agent loop, conversations and the usage log. Each plugin is given its own
/// <c>IAi</c> through <see cref="IPluginAiHost"/>.
/// </summary>
public sealed class AiModule : IHostPluginModule
{
    /// <summary>The named HttpClient the drivers send with (no retries of its own: failover is the SDK's).</summary>
    public const string HTTP_CLIENT = "turbo-ai";

    public string Key => "turbo-ai";

    public void ConfigureServices(IServiceCollection services, HostApplicationBuilder builder)
    {
        services.Configure<AiConfig>(builder.Configuration.GetSection(AiConfig.SECTION_NAME));
        services.AddHttpClient(
            HTTP_CLIENT,
            http => http.Timeout = System.Threading.Timeout.InfiniteTimeSpan
        );

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IAiDriver, AnthropicDriver>();
        services.AddSingleton<IAiDriver, OpenAiCompatibleDriver>();
        services.TryAddSingleton<IAiStore, DbAiStore>();
        // The hotel's approvals (docs/approvals.md): an agent's tool that changes things waits on one.
        services.TryAddSingleton<IAiApprovals, HotelAiApprovals>();
        services.AddSingleton<IApprovalHandler, AiToolApprovalHandler>();
        services.TryAddSingleton<IAiPermissions, HotelAiPermissions>();
        // Runs a minute counted for the whole cluster: one grain per player and per plugin.
        services.AddSingleton<IAiRateLimits, ClusterAiRateLimits>();
        services.AddSingleton<AiProviders>();
        services.AddSingleton<AiRuntime>();
        services.AddSingleton<AiHost>();
        services.AddSingleton<IPluginAiHost>(sp => sp.GetRequiredService<AiHost>());
        services.AddSingleton<IAiOverview>(sp => sp.GetRequiredService<AiHost>());
    }
}
