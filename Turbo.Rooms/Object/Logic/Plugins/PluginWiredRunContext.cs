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
        new(
            avatar.ObjectId.Value,
            avatar switch
            {
                IRoomPlayer => RoomUserKind.Player,
                IRoomPet => RoomUserKind.Pet,
                _ => RoomUserKind.Bot,
            },
            (avatar as IRoomPlayer)?.PlayerId,
            avatar.Name,
            avatar.X,
            avatar.Y,
            avatar.Rotation,
            avatar.IsWalking,
            avatar.IsIdle,
            avatar.EffectId
        );

    public static RoomFurni Furni(IRoomFloorItem item) =>
        new(
            item.ObjectId.Value,
            item.Definition.LogicName,
            item.X,
            item.Y,
            item.Z.Value,
            item.Logic.GetState(),
            item.Rotation,
            item.Definition
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
    Func<RoomUser, string, RoomSpeech, CancellationToken, Task<bool>> say
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

    public Task<bool> SayAsync(
        RoomUser user,
        string text,
        RoomSpeech speech,
        CancellationToken ct
    ) => say(user, text, speech, ct);
}
