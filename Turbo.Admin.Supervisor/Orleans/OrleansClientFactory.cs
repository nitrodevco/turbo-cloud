using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Orleans;
using Orleans.Hosting;

namespace Turbo.Admin.Supervisor.Orleans;

/// <summary>
/// A freshly-built, not-yet-started <see cref="IClusterClient"/> plus the isolated <see cref="ServiceProvider"/>
/// that owns it. The provider must be disposed once the client is no longer needed (failed to start, or
/// disconnected) - the client itself exposes no disposal surface, but its backing services (connection
/// retry timers, sockets, etc.) are only released when the provider is.
/// </summary>
internal sealed record OrleansClientHandle(IClusterClient Client, ServiceProvider Provider) : IDisposable
{
    public void Dispose() => Provider.Dispose();
}

/// <summary>
/// Builds a fresh <see cref="IClusterClient"/> against an isolated <see cref="IServiceCollection"/>, so it
/// stays independent of the app's own DI container (which would otherwise also register Orleans' auto-connecting
/// hosted service - that blocks or throws on host startup if the silo isn't reachable yet). Each call produces
/// a brand new, not-yet-started client; <see cref="OrleansClientConnector"/> owns starting it and rebuilding a
/// new one on failure or disconnect, rather than ever reusing a client instance across attempts.
/// </summary>
internal static class OrleansClientFactory
{
    public static OrleansClientHandle Create(
        IConfiguration configuration,
        int gatewayPort,
        Action onConnectionLost
    )
    {
        var services = new ServiceCollection();
        var clientBuilder = new ClientBuilder(services, configuration);

        clientBuilder.UseLocalhostClustering(gatewayPort: gatewayPort);
        clientBuilder.AddClusterConnectionLostHandler((_, _) => onConnectionLost());

        var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IClusterClient>();

        return new OrleansClientHandle(client, provider);
    }
}
