using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Rooms;
using Turbo.Contracts.Wired;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Plugins;

/// <summary>
/// The host of a plugin's wired box: a box of core's kind (trigger, effect, condition, selector)
/// that takes its declaration from the plugin's <see cref="Contracts.Wired.WiredBox"/> and runs it.
/// It has no <c>[RoomObjectLogic]</c> of its own: the plugin's key is registered for it.
/// </summary>
internal interface IPluginWiredHost
{
    WiredBox Box { get; }
}

/// <summary>Room objects as the contracts show them to a plugin.</summary>
internal static class PluginRoomViews
{
    public static RoomUser User(IRoomAvatar avatar) =>
        avatar switch
        {
            IRoomPlayer player => new RoomUser(
                avatar.ObjectId.Value,
                RoomUserKind.Player,
                player.PlayerId,
                avatar.Name,
                avatar.X,
                avatar.Y
            ),
            IRoomPet => new RoomUser(
                avatar.ObjectId.Value,
                RoomUserKind.Pet,
                null,
                avatar.Name,
                avatar.X,
                avatar.Y
            ),
            _ => new RoomUser(
                avatar.ObjectId.Value,
                RoomUserKind.Bot,
                null,
                avatar.Name,
                avatar.X,
                avatar.Y
            ),
        };

    public static RoomFurni Furni(IRoomFloorItem item) =>
        new(
            item.ObjectId.Value,
            item.Definition.LogicName,
            item.X,
            item.Y,
            item.Z.Value,
            item.Logic.GetState()
        );
}

internal sealed class PluginWiredBoxConfig(
    string text,
    Func<int, int> param,
    IReadOnlyList<int> pickedFurni
) : IWiredBoxConfig
{
    public string Text { get; } = text;

    public int Param(int index) => param(index);

    public IReadOnlyList<int> PickedFurni { get; } = pickedFurni;
}

/// <summary>
/// What a plugin box runs with. The room's users and furni are read when asked for, so a box
/// that never looks at them costs nothing.
/// </summary>
internal sealed class PluginWiredRunContext(
    RoomId room,
    IWiredBoxConfig config,
    IReadOnlyList<RoomUser> users,
    IReadOnlyList<RoomFurni> furni,
    Func<IReadOnlyList<RoomUser>> roomUsers,
    Func<IReadOnlyList<RoomFurni>> roomFurni,
    IWiredExecutionContext? execution,
    Func<RoomUser, string, bool, CancellationToken, Task> showMessage
) : IWiredEffectContext
{
    private IReadOnlyList<RoomUser>? _roomUsers;
    private IReadOnlyList<RoomFurni>? _roomFurni;

    public RoomId Room { get; } = room;

    public string Text => config.Text;

    public int Param(int index) => config.Param(index);

    public IReadOnlyList<int> PickedFurni => config.PickedFurni;

    public IReadOnlyList<RoomUser> Users { get; } = users;

    public IReadOnlyList<RoomFurni> Furni { get; } = furni;

    public IReadOnlyList<RoomUser> RoomUsers => _roomUsers ??= roomUsers();

    public IReadOnlyList<RoomFurni> RoomFurni => _roomFurni ??= roomFurni();

    public Task<string> FormatTextAsync(string text, CancellationToken ct) =>
        execution is null ? Task.FromResult(text) : execution.FormatTextAsync(text, ct);

    public Task ShowMessageAsync(
        RoomUser user,
        string text,
        bool onlyForThem,
        CancellationToken ct
    ) => showMessage(user, text, onlyForThem, ct);
}
