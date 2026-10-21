using Microsoft.Extensions.DependencyInjection;
using Orleans;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Object.Logic.Avatars;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Object.Avatars.Player;
using Turbo.Rooms.Object.Furniture.Floor;

namespace Turbo.Tests.Support;

/// <summary>
/// A live room (every module and the wired system real) with the things a wired test needs to
/// stand in it: users with a logic and a context, floor furni, and wired boxes saved the way the
/// client's editor saves them.
/// </summary>
public sealed class WiredRoom
{
    public WiredRoom(int width = 8, int height = 8, RoomConfig? roomConfig = null)
    {
        Harness = new LiveRoomHarness(width, height, roomConfig);
        Harness.State.IsRightsLoaded = true;

        // A furni told that an avatar walked on it asks the avatar's logic for its context.
        Harness.Fakes.Handlers["get_Context"] = call =>
            call.Interface == typeof(IRoomPlayerLogic)
                ? Harness.Fakes.Create<IRoomPlayerContext>(call.Key)
                : Fakes.NotHandled;
        // The room's own wired variables (user count, furni count...) are not what these tests are about.
        Harness.Fakes.Handlers["BuildVariablesForRoom"] = _ => Array.Empty<IWiredVariable>();
        Harness.Fakes.Handlers["get_ObjectId"] = call =>
            call.Interface == typeof(IRoomPlayerContext) && call.Key is int id
                ? (RoomObjectId)id
                : Fakes.NotHandled;
    }

    public LiveRoomHarness Harness { get; }

    public Dictionary<int, RoomPlayerAvatar> Avatars { get; } = [];

    public RoomMapModule Map => Harness.Module<RoomMapModule>();

    public IGrainFactory Grains => Harness.Services.GetRequiredService<IGrainFactory>();

    public IStuffDataFactory StuffData => Harness.Services.GetRequiredService<IStuffDataFactory>();

    /// <summary>A floor furni already in the room, by object id.</summary>
    public IRoomFloorItem FloorItem(int id) =>
        (IRoomFloorItem)Harness.State.ItemsById.GetValueOrDefault((RoomObjectId)id)!;

    public RoomTileFlags[] TileFlags() => Harness.State.TileFlags;

    /// <summary>Where each user stands, as <c>id@x,y</c>, in id order.</summary>
    public string[] Positions() =>
        [.. Avatars.OrderBy(a => a.Key).Select(a => $"{a.Key}@{a.Value.X},{a.Value.Y}")];

    public RoomPlayerAvatar Enter(int objectId, int x, int y)
    {
        var avatar = new RoomPlayerAvatar { ObjectId = objectId, PlayerId = 100 + objectId };

        avatar.SetPosition(x, y);
        avatar.SetLogic(Harness.Fakes.Create<IRoomPlayerLogic>(objectId));
        Avatars[objectId] = avatar;
        Harness.State.AvatarsByObjectId[objectId] = avatar;
        Harness.State.AvatarsByPlayerId[100 + objectId] = objectId;
        Map.AddAvatar(avatar, false);

        return avatar;
    }

    /// <summary>
    /// A floor furni on a tile. Two with the same <paramref name="definitionId"/> are the same
    /// kind of furni; without one each is its own kind.
    /// </summary>
    public IRoomFloorItem AddFloorItem(
        int id,
        int x,
        int y,
        string logicName = "default_floor",
        int? definitionId = null,
        bool canWalk = true,
        Func<IStuffDataFactory, IRoomFloorItemContext, IRoomObjectLogic>? createLogic = null,
        FurnitureUsageType usagePolicy = FurnitureUsageType.Everybody,
        bool canSit = false,
        bool canLay = false,
        string? customParams = null,
        int totalStates = 2
    )
    {
        var item = new RoomFloorItem
        {
            ObjectId = id,
            OwnerId = 1,
            OwnerName = "test",
            Definition = new FurnitureDefinitionSnapshot
            {
                Id = definitionId ?? 1000 + id,
                SpriteId = definitionId ?? 1000 + id,
                Name = logicName,
                ProductType = ProductType.Floor,
                FurniCategory = FurnitureCategory.Default,
                LogicName = logicName,
                TotalStates = totalStates,
                Width = 1,
                Length = 1,
                StackHeight = Altitude.FromInt(100),
                CanStack = true,
                CanWalk = canWalk,
                CanSit = canSit,
                CanLay = canLay,
                CanRecycle = true,
                CanTrade = true,
                CanGroup = true,
                CanSell = true,
                UsagePolicy = usagePolicy,
                ExtraData = null,
                CustomParams = customParams,
            },
        };

        item.SetExtraData(null);
        item.SetPosition(x, y);
        item.SetPositionZ(Altitude.Zero);
        item.SetRotation(Rotation.North);

        var context = new RoomFloorItemContext(Harness.Room, item);

        item.SetLogic(
            createLogic?.Invoke(StuffData, context)
                ?? new Turbo.Rooms.Object.Logic.Furniture.Floor.FurnitureFloorLogic(
                    StuffData,
                    context
                )
        );

        Harness.State.ItemsById.Add(id, item);
        Map.AddItem(item);

        return item;
    }

    /// <summary>A plain wall item (a poster, a wall lamp) on the room's wall.</summary>
    public Turbo.Rooms.Object.Furniture.Wall.RoomWallItem AddWallItem(int id)
    {
        var item = new Turbo.Rooms.Object.Furniture.Wall.RoomWallItem
        {
            ObjectId = id,
            OwnerId = 1,
            OwnerName = "test",
            Definition = new FurnitureDefinitionSnapshot
            {
                Id = 2000 + id,
                SpriteId = 2000 + id,
                Name = "wall_lamp",
                ProductType = ProductType.Wall,
                FurniCategory = FurnitureCategory.Default,
                LogicName = "default_wall",
                TotalStates = 2,
                Width = 1,
                Length = 1,
                StackHeight = Altitude.Zero,
                CanStack = false,
                CanWalk = false,
                CanSit = false,
                CanLay = false,
                CanRecycle = true,
                CanTrade = true,
                CanGroup = true,
                CanSell = true,
                UsagePolicy = FurnitureUsageType.Everybody,
                ExtraData = null,
            },
        };

        item.SetExtraData(null);
        item.SetLogic(
            new Turbo.Rooms.Object.Logic.Furniture.Wall.FurnitureWallLogic(
                StuffData,
                new Turbo.Rooms.Object.Furniture.Wall.RoomWallItemContext(Harness.Room, item)
            )
        );
        Harness.State.ItemsById.Add(id, item);

        return item;
    }

    /// <summary>A wired box of logic type <typeparamref name="T"/> standing on a tile.</summary>
    public T AddBox<T>(int id, int x, int y, string logicName)
        where T : class, IRoomObjectLogic
    {
        T? box = null;

        AddFloorItem(
            id,
            x,
            y,
            logicName,
            createLogic: (factory, ctx) =>
                box = (T)Activator.CreateInstance(typeof(T), Grains, factory, ctx)!
        );

        return box!;
    }

    /// <summary>
    /// What the client's editor sends when "ready" is pressed, run through the room as the
    /// packet handler does. <c>false</c> when the room refuses the save.
    /// </summary>
    public async Task<bool> SaveAsync<TMessage>(
        int boxId,
        int[]? intParams = null,
        int[]? stuffIds = null,
        int[]? stuffIds2 = null,
        WiredFurniSourceType[][]? furniSources = null,
        WiredPlayerSourceType[][]? playerSources = null,
        object[]? definitionSpecifics = null,
        string stringParam = "",
        string[]? variableIds = null
    )
        where TMessage : UpdateWiredMessage =>
        (
            await SaveWithResultAsync<TMessage>(
                boxId,
                intParams,
                stuffIds,
                stuffIds2,
                furniSources,
                playerSources,
                definitionSpecifics,
                stringParam,
                variableIds
            )
        ).IsSaved;

    /// <summary>
    /// <see cref="SaveAsync"/> with what the editor is told, a refusal's text included; saved by
    /// <paramref name="actor"/> (the system when null).
    /// </summary>
    public Task<WiredSaveResult> SaveWithResultAsync<TMessage>(
        int boxId,
        int[]? intParams = null,
        int[]? stuffIds = null,
        int[]? stuffIds2 = null,
        WiredFurniSourceType[][]? furniSources = null,
        WiredPlayerSourceType[][]? playerSources = null,
        object[]? definitionSpecifics = null,
        string stringParam = "",
        string[]? variableIds = null,
        ActionContext? actor = null
    )
        where TMessage : UpdateWiredMessage
    {
        var message = (TMessage)Activator.CreateInstance(typeof(TMessage))! with
        {
            Id = boxId,
            IntParams = [.. intParams ?? []],
            StringParam = stringParam,
            StuffIds = [.. stuffIds ?? []],
            StuffIds2 = [.. stuffIds2 ?? []],
            DefinitionSpecifics = [.. definitionSpecifics ?? []],
            FurniSources = [.. furniSources ?? []],
            PlayerSources = [.. playerSources ?? []],
            VariableIds = [.. variableIds ?? []],
            TypeSpecifics = [],
        };

        return Harness.Room.ApplyWiredUpdateAsync(
            actor ?? ActionContext.CreateForSystem(1),
            boxId,
            message,
            CancellationToken.None
        );
    }
}
