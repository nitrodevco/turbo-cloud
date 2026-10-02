using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Orleans.Configuration;
using Turbo.Main.Extensions;
using Xunit;

namespace Turbo.Tests.Networking;

public sealed class OrleansEndpointTests
{
    [Fact]
    public void ConfiguredPortsAreBothListenedOnAndAdvertised()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Turbo:Orleans:SiloAddress"] = "127.0.0.1",
                ["Turbo:Orleans:SiloPort"] = "12111",
                ["Turbo:Orleans:GatewayPort"] = "3200",
            }
        );
        builder.AddOrleans();
        using var host = builder.Build();
        var endpoints = host.Services.GetRequiredService<IOptions<EndpointOptions>>().Value;
        Assert.Equal(12111, endpoints.SiloPort);
        Assert.Equal(3200, endpoints.GatewayPort);
        Assert.Equal(endpoints.SiloPort, endpoints.SiloListeningEndpoint!.Port);
        Assert.Equal(endpoints.GatewayPort, endpoints.GatewayListeningEndpoint!.Port);
    }
}
