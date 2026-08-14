using System;
using Microsoft.Extensions.Hosting;
using Orleans.Configuration;
using Orleans.Hosting;
using Turbo.Primitives.Orleans;

namespace Turbo.Main.Extensions;

public static class HostApplicationBuilderExtensions
{
    public static HostApplicationBuilder AddOrleans(this HostApplicationBuilder builder)
    {
        builder.UseOrleans(
            (System.Action<ISiloBuilder>)(
                silo =>
                {
                    silo.Configure<GrainCollectionOptions>(options =>
                    {
                        options.CollectionAge = TimeSpan.FromMinutes(2);
                    });
                    silo.ConfigureEndpoints(
                        "127.0.0.1",
                        siloPort: 11111,
                        gatewayPort: 3000,
                        listenOnAnyHostAddress: false
                    );

                    // UseLocalhostClustering() also configures endpoints internally, using its own
                    // defaults (siloPort 11111, gatewayPort 30000) unless told otherwise - its default
                    // gateway port collides with the game TCP socket (also 30000, see NetworkManager),
                    // so the ports above must be passed through explicitly here too.
                    silo.UseLocalhostClustering(siloPort: 11111, gatewayPort: 3000)
                        .AddMemoryGrainStorage(OrleansStorageNames.PUB_SUB_STORE)
                        .AddMemoryGrainStorage(OrleansStorageNames.PLAYER_STORE)
                        .AddMemoryGrainStorage(OrleansStorageNames.ROOM_STORE)
                        .AddMemoryStreams(OrleansStreamProviders.DEFAULT_STREAM_PROVIDER)
                        .AddMemoryStreams(OrleansStreamProviders.ROOM_STREAM_PROVIDER);
                }
            )
        );

        return builder;
    }
}
