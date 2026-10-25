using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Contracts.Hotel;
using Turbo.Plugins;
using Turbo.Plugins.Configuration;
using Turbo.Primitives.Plugins;
using Turbo.Runtime.AssemblyProcessing;
using Turbo.Testing;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Hotel;

/// <summary>How plugins reach the hotel API, and the rule every member of it keeps.</summary>
public sealed class HotelApiTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(),
        "turbo-hotel-api-" + Guid.NewGuid().ToString("N")
    );

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }

    /// <summary>
    /// Where a member may be called from is part of its contract (docs/extensions.md, rules every
    /// kind follows): an author awaiting a database read in a room's turn stalls the room.
    /// </summary>
    [Fact]
    public void Every_member_of_the_hotel_api_says_where_it_may_be_called_from()
    {
        Type[] areas =
        [
            .. typeof(IHotel)
                .GetProperties()
                .Select(x => x.PropertyType)
                .Where(x => x.Namespace == typeof(IHotel).Namespace && x.IsInterface),
        ];

        areas.Should().HaveCount(8);

        var unmarked = areas
            .SelectMany(area => area.GetMethods().Select(m => $"{area.Name}.{m.Name}"))
            .Except(
                areas.SelectMany(area =>
                    area.GetMethods()
                        .Where(m => m.GetCustomAttribute<HotelCallAttribute>() is not null)
                        .Select(m => $"{area.Name}.{m.Name}")
                        // A property says it once, for its getter.
                        .Concat(
                            area.GetProperties()
                                .Where(p => p.GetCustomAttribute<HotelCallAttribute>() is not null)
                                .Select(p => $"{area.Name}.get_{p.Name}")
                        )
                )
            );

        unmarked.Should().BeEmpty("each says Anywhere, NotFromItsGrain or OffGrain");
    }

    /// <summary>
    /// A plugin's own services are built by its own container, not the host's: the host puts the
    /// hotel API in it, so a service registered in <c>ConfigureServices</c> takes it by constructor.
    /// </summary>
    [Fact]
    public async Task A_plugins_own_service_takes_the_hotel_by_constructor()
    {
        var hotel = new FakeHotel();
        var alice = hotel.Players.Add("Alice", online: true);

        PluginBuilder.Write(_folder, "greeter", GREETER);

        var manager = new PluginManager(
            new ServiceCollection().AddSingleton<IHotel>(hotel).BuildServiceProvider(),
            new AssemblyProcessor([]),
            Options.Create(new PluginConfig { PluginFolderPath = _folder }),
            NullLogger<PluginManager>.Instance
        );

        await manager.LoadAllAsync(ct: Ct);

        var status = manager.Snapshot().Single();
        status.State.Should().Be(PluginStateType.Loaded, status.Error);
        hotel.Wallet.AssertCredited(alice, "credits", 5);

        await manager.UnloadAllAsync(Ct);
    }

    private const string GREETER = """
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using Microsoft.Extensions.DependencyInjection;
        using Turbo.Contracts.Hotel;
        using Turbo.Contracts.Plugins;
        using Turbo.Contracts.Plugins.Exports;
        using Turbo.Primitives.Players;

        public sealed class Greeter(IHotel hotel)
        {
            public Task GreetAsync(CancellationToken ct) =>
                hotel.Wallet.CreditAsync(new PlayerId(1), "credits", 5, "greeter:1", "greeter.welcome", ct);
        }

        public sealed class GreeterPlugin : ITurboPlugin
        {
            public string Key => "greeter";
            public string Version => "1.0.0";
            public void ConfigureServices(IServiceCollection services, PluginManifest manifest) =>
                services.AddSingleton<Greeter>();
            public Task StartAsync(IServiceProvider services, CancellationToken ct) =>
                services.GetRequiredService<Greeter>().GreetAsync(ct);
            public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
            public Task BindExportsAsync(IExportBinder binder, IServiceProvider services) => Task.CompletedTask;
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
        """;
}
