using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Orleans.Configuration;
using Orleans.Hosting;
using Turbo.Main.Configuration;
using Turbo.Primitives.Orleans;

namespace Turbo.Main.Extensions;

public static class HostApplicationBuilderExtensions
{
    public static HostApplicationBuilder AddOrleans(this HostApplicationBuilder builder)
    {
        var orleansConfig =
            builder.Configuration.GetSection(OrleansConfig.SECTION_NAME).Get<OrleansConfig>()
            ?? new OrleansConfig();

        builder.UseOrleans(
            (System.Action<ISiloBuilder>)(
                silo =>
                {
                    silo.Configure<GrainCollectionOptions>(options =>
                    {
                        options.CollectionAge = TimeSpan.FromMinutes(
                            orleansConfig.GrainCollectionAgeMinutes
                        );
                    });
                    silo.ConfigureEndpoints(
                        orleansConfig.SiloAddress,
                        siloPort: orleansConfig.SiloPort,
                        gatewayPort: orleansConfig.GatewayPort,
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
                                            orleansConfig.RoomStreamPollMs
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
