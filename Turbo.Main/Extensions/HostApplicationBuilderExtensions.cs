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
                        listenOnAnyHostAddress: true
                    );

                    silo.UseLocalhostClustering()
                        .AddMemoryGrainStorage(OrleansStorageNames.PUB_SUB_STORE)
                        .AddMemoryGrainStorage(OrleansStorageNames.PLAYER_STORE)
                        .AddMemoryGrainStorage(OrleansStorageNames.ROOM_STORE)
                        .AddMemoryStreams(OrleansStreamProviders.DEFAULT_STREAM_PROVIDER)
                        .AddMemoryStreams(
                            OrleansStreamProviders.ROOM_STREAM_PROVIDER,
                            streams =>
                                streams.ConfigurePullingAgent(ob =>
                                    ob.Configure(options =>
                                    {
                                        // Memory streams are pull-based; the default 100ms poll
                                        // adds up to 100ms of jitter to every room packet, which
                                        // is visible in the avatar walk cadence.
                                        options.GetQueueMsgsTimerPeriod = TimeSpan.FromMilliseconds(
                                            10
                                        );
                                    })
                                )
                        );
                }
            )
        );

        return builder;
    }
}
