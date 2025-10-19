using System;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Hosting;
using Orleans.Hosting;

namespace Turbo.Grains.Extensions;

public static class HostApplicationBuilderExtensions
{
    public static HostApplicationBuilder AddTurboGrains(this HostApplicationBuilder builder)
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
                .AddMemoryGrainStorage("PubSubStore")
                .AddMemoryGrainStorage("player-snapshots");
        });

        return builder;
    }
}
