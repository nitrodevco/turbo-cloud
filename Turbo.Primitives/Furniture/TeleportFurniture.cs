using System;
using System.Collections.Generic;
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

    /// <summary>Whether a furni comes as a linked pair: a teleporter, or a room linker by its classname.</summary>
    public static bool IsLinkedPair(string? logicName, string? classname) =>
        IsTeleport(logicName)
        || string.Equals(classname, ROOM_LINKER_CLASSNAME, StringComparison.Ordinal);

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
