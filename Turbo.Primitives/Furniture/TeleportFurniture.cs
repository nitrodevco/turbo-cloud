using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.Json;
using Turbo.Primitives.Furniture.ExtraData;

namespace Turbo.Primitives.Furniture;

/// <summary>
/// Teleporters: furni sold in linked pairs, each half leading to the other wherever it stands.
/// Here rather than in the room module because the catalog has to recognise one, and pair it,
/// before either half exists in a room.
/// </summary>
public static class TeleportFurniture
{
    public const string LOGIC_NAME = "teleport";

    /// <summary>
    /// "WIRED Room Linker" (<c>wf_room_linker</c>): sold in linked pairs like a teleporter, and
    /// used through "WIRED Effect: Teleport to Room", which sends users to the room its other half
    /// stands in (Wired Faculty tutorial "New WIRED Room Linker Tutorial", 30/04/2025).
    /// </summary>
    public const string ROOM_LINKER_CLASSNAME = "wf_room_linker";

    public static bool IsTeleport(string? logicName) =>
        string.Equals(logicName, LOGIC_NAME, StringComparison.Ordinal);

    public static bool IsRoomLinker(string? classname) =>
        string.Equals(classname, ROOM_LINKER_CLASSNAME, StringComparison.Ordinal);

    // The logic keys a loaded plugin's furni logic sells as linked pairs ([FurniLogic(..., LinkedPair
    // = true)]), once per registration: two plugins naming one key both hold it.
    private static ImmutableList<string> _pairedLogics = [];

    /// <summary>
    /// Whether a furni comes as a linked pair: a teleporter, a room linker by its classname, or a
    /// furni whose logic a loaded plugin registered as one (<see cref="AddPairedLogic"/>).
    /// </summary>
    public static bool IsLinkedPair(string? logicName, string? classname) =>
        IsTeleport(logicName)
        || IsRoomLinker(classname)
        || (logicName is not null && _pairedLogics.Contains(logicName));

    /// <summary>
    /// Sells, gives and grants the furni whose logic is <paramref name="logicName"/> as linked
    /// pairs, as teleporters are, until the result is disposed (its plugin unloads).
    /// </summary>
    public static IDisposable AddPairedLogic(string logicName)
    {
        ImmutableInterlocked.Update(ref _pairedLogics, list => list.Add(logicName));

        return new PairedLogic(logicName);
    }

    private sealed class PairedLogic(string logicName) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (System.Threading.Interlocked.Exchange(ref _disposed, 1) == 0)
                ImmutableInterlocked.Update(
                    ref _pairedLogics,
                    list => list.Remove(logicName, StringComparer.Ordinal)
                );
        }
    }

    /// <summary>
    /// The extra data one half of a new pair starts with: a <see cref="RoomLinkerData"/> naming
    /// the other half. Only the item is named, never a room — the pair can be picked up and
    /// placed anywhere, so where it leads is looked up when it is used.
    /// </summary>
    public static string PairExtraData(int partnerItemId) =>
        JsonSerializer.Serialize(
            new Dictionary<string, object>
            {
                [RoomLinkerData.SECTION] = new RoomLinkerData { ItemId = partnerItemId },
            }
        );
}

/// <summary>
/// A teleporter's states as the client draws them: shut, open for someone to step in or out,
/// and flashing while it sends or receives.
/// </summary>
public static class TeleportStates
{
    public const int CLOSED = 0;
    public const int OPEN = 1;
    public const int ACTIVE = 2;
}
