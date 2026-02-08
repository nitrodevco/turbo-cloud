using Orleans;
using Turbo.Primitives.Catalog.Grains;
using Turbo.Primitives.FriendList.Grains;
using Turbo.Primitives.Grains.Players;
using Turbo.Primitives.Inventory.Grains;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Grains;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Grains;

namespace Turbo.Primitives.Orleans;

public static class GrainFactoryExtensions
{
    public static IRoomGrain GetRoomGrain(this IGrainFactory factory, RoomId roomId) =>
        factory.GetGrain<IRoomGrain>((long)roomId.Value);

    public static IRoomPersistenceGrain GetRoomPersistenceGrain(
        this IGrainFactory factory,
        RoomId roomId
    ) => factory.GetGrain<IRoomPersistenceGrain>((long)roomId.Value);

    public static IRoomDirectoryGrain GetRoomDirectoryGrain(this IGrainFactory factory) =>
        factory.GetGrain<IRoomDirectoryGrain>(SingletonGrainId.GLOBAL);

    public static IPlayerGrain GetPlayerGrain(this IGrainFactory factory, PlayerId playerId) =>
        factory.GetGrain<IPlayerGrain>((long)playerId.Value);

    public static IPlayerPresenceGrain GetPlayerPresenceGrain(
        this IGrainFactory factory,
        PlayerId playerId
    ) => factory.GetGrain<IPlayerPresenceGrain>(playerId.Value);

    public static IPlayerPresenceGrain GetPlayerPresenceGrain(
        this IGrainFactory factory,
        long playerId
    ) => factory.GetGrain<IPlayerPresenceGrain>(playerId);

    public static IPlayerDirectoryGrain GetPlayerDirectoryGrain(this IGrainFactory factory) =>
        factory.GetGrain<IPlayerDirectoryGrain>(SingletonGrainId.GLOBAL);

    public static IPlayerWalletGrain GetPlayerWalletGrain(
        this IGrainFactory factory,
        PlayerId playerId
    ) => factory.GetGrain<IPlayerWalletGrain>(playerId.Value);

    public static IPlayerWalletGrain GetPlayerWalletGrain(
        this IGrainFactory factory,
        long playerId
    ) => factory.GetGrain<IPlayerWalletGrain>(playerId);

    public static IInventoryGrain GetInventoryGrain(
        this IGrainFactory factory,
        PlayerId playerId
    ) => factory.GetGrain<IInventoryGrain>(playerId.Value);

    public static IInventoryGrain GetInventoryGrain(this IGrainFactory factory, long playerId) =>
        factory.GetGrain<IInventoryGrain>(playerId);

    public static ICatalogPurchaseGrain GetCatalogPurchaseGrain(
        this IGrainFactory factory,
        PlayerId playerId
    ) => factory.GetGrain<ICatalogPurchaseGrain>(playerId.Value);

    public static ICatalogPurchaseGrain GetCatalogPurchaseGrain(
        this IGrainFactory factory,
        long playerId
    ) => factory.GetGrain<ICatalogPurchaseGrain>(playerId);

    public static IMessengerGrain GetMessengerGrain(
        this IGrainFactory factory,
        PlayerId playerId
    ) => factory.GetGrain<IMessengerGrain>(playerId.Value);

    public static IMessengerGrain GetMessengerGrain(
        this IGrainFactory factory,
        long playerId
    ) => factory.GetGrain<IMessengerGrain>(playerId);
}
