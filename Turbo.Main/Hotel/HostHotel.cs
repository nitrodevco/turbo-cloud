using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Turbo.Contracts.Hotel;
using Turbo.Plugins;
using Turbo.Primitives.Rooms.Watching;

namespace Turbo.Main.Hotel;

/// <summary>The hotel API (<see cref="IHotel"/>) over the host's own services, one per area.</summary>
internal sealed class HostHotel(
    IHotelPlayers players,
    IHotelRooms rooms,
    IHotelWallet wallet,
    IHotelInventory inventory,
    IHotelCatalog catalog,
    IHotelNotify notify,
    IHotelNavigator navigator,
    IHotelWired wired,
    IHotelStatus status,
    TimeProvider clock
) : IHotel, IPluginBoundHotel
{
    public IHotelPlayers Players { get; } = players;

    public IHotelRooms Rooms { get; } = rooms;

    public IHotelWallet Wallet { get; } = wallet;

    public IHotelInventory Inventory { get; } = inventory;

    public IHotelCatalog Catalog { get; } = catalog;

    public IHotelNotify Notify { get; } = notify;

    public IHotelNavigator Navigator { get; } = navigator;

    public IHotelWired Wired { get; } = wired;

    public IHotelStatus Status { get; } = status;

    public TimeProvider Clock { get; } = clock;

    public IHotel ForPlugin(string pluginKey) =>
        new HostHotel(
            Players,
            Rooms is HostHotelRooms rooms ? rooms.ForPlugin(pluginKey) : Rooms,
            Wallet,
            Inventory,
            Catalog,
            Notify,
            Navigator,
            Wired,
            Status,
            Clock
        );
}

public static class HotelServiceCollectionExtensions
{
    /// <summary>
    /// The hotel API, which every plugin's container is given (<c>docs/hotel-api.md</c>). Each area
    /// is registered on its own, so the host (or a test) can put another implementation of one
    /// area in front without the rest.
    /// </summary>
    public static IServiceCollection AddTurboHotel(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IHotelPlayers, HostHotelPlayers>();
        services.TryAddSingleton<IHotelRooms, HostHotelRooms>();
        services.TryAddSingleton<IHotelWallet, HostHotelWallet>();
        services.TryAddSingleton<IHotelInventory, HostHotelInventory>();
        services.TryAddSingleton<IHotelCatalog, HostHotelCatalog>();
        services.TryAddSingleton<IHotelNotify, HostHotelNotify>();
        services.TryAddSingleton<HostHotelPublicRooms>();
        services.TryAddSingleton<IHotelNavigator, HostHotelNavigator>();
        services.TryAddSingleton<IHotelWired, HostHotelWired>();
        services.TryAddSingleton<IHotelStatus, HostHotelStatus>();
        services.TryAddSingleton<PluginRoomLeases>();
        services.TryAddSingleton<IPluginRoomLeases>(sp =>
            sp.GetRequiredService<PluginRoomLeases>()
        );
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IPluginStopping, PluginRoomLeases>(sp =>
                sp.GetRequiredService<PluginRoomLeases>()
            )
        );
        services.TryAddSingleton<IHotel, HostHotel>();

        return services;
    }
}
