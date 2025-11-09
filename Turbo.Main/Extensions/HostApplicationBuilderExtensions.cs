using Microsoft.Extensions.Hosting;
using Orleans.Hosting;
using Orleans.Streams;
using Turbo.Contracts.Orleans;

namespace Turbo.Main.Extensions;

public static class HostApplicationBuilderExtensions
{
    public static HostApplicationBuilder AddOrleans(this HostApplicationBuilder builder)
    {
        builder.UseOrleans(silo =>
        {
            silo.ConfigureEndpoints(
                "127.0.0.1",
                siloPort: 11111,
                gatewayPort: 3000,
                listenOnAnyHostAddress: true
            );

            silo.UseLocalhostClustering()
                .AddMemoryGrainStorage(OrleansStorageNames.PRESENCE_STORE)
                .AddMemoryGrainStorage(OrleansStorageNames.PUB_SUB_STORE)
                .AddMemoryGrainStorage(OrleansStorageNames.PLAYER_STORE)
                .AddMemoryStreams(OrleansStreamProviders.DEFAULT_STREAM_PROVIDER);
        });

        return builder;
    }
}
