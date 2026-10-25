using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Rooms;
using Turbo.Primitives.Rooms;

namespace Turbo.Contracts.Wired;

/// <summary>How a box is saved: its text, its int params and its picked furni.</summary>
public interface IWiredBoxConfig
{
    /// <summary>The saved text, trimmed; empty when there is none.</summary>
    string Text { get; }

    /// <summary>Int param <paramref name="index"/> as saved (checked by its rule), or its default.</summary>
    int Param(int index);

    /// <summary>The picked furni that are still in the room, by id.</summary>
    IReadOnlyList<int> PickedFurni { get; }
}

/// <summary>
/// A box running in its room's turn. Everything here is the room's own and in memory; do not await
/// the hotel API's calls on this room (<c>IHotel.Rooms</c> with this room) from a box: the room
/// would wait on itself. A call on another grain (a player's wallet, inventory) is fine.
/// </summary>
public interface IWiredRunContext : IWiredBoxConfig
{
    RoomId Room { get; }

    /// <summary>The users the box acts on, from the source the player picked (<see cref="WiredBox.ActsOnUsers"/>).</summary>
    IReadOnlyList<RoomUser> Users { get; }

    /// <summary>The furni the box acts on, from the source the player picked (<see cref="WiredBox.ActsOnFurni"/>).</summary>
    IReadOnlyList<RoomFurni> Furni { get; }

    /// <summary>Everyone in the room: players, pets and bots.</summary>
    IReadOnlyList<RoomUser> RoomUsers { get; }

    /// <summary>Every floor furni in the room.</summary>
    IReadOnlyList<RoomFurni> RoomFurni { get; }
}

/// <summary>What an effect can do in its room, through the room's own rules.</summary>
public interface IWiredEffectContext : IWiredRunContext
{
    /// <summary><paramref name="text"/> with the stack's placeholders filled in (the placeholder add-ons).</summary>
    Task<string> FormatTextAsync(string text, CancellationToken ct);

    /// <summary>
    /// A chat bubble over <paramref name="user"/> (Show Message's): the whole room sees it, or only
    /// that user when <paramref name="onlyForThem"/> (a whisper). Pets are skipped.
    /// </summary>
    Task ShowMessageAsync(RoomUser user, string text, bool onlyForThem, CancellationToken ct);
}
