namespace Turbo.Admin.Api.Contracts;

/// <summary>What the signed-in staff member may do to one room from the panel: the same nodes the hotel asks for (owning it or <c>room.control.any</c> for its settings, rights and the bots and pets standing in it, <c>room.moderate.any</c> for kicks, mutes and bans, each room command's own node, <c>admin.rooms.transfer</c> to give the room to another player, and owning it or <c>room.furni.pickup_any</c> to take its furniture back to their owners).</summary>
public sealed record RoomAbilities(
    bool EditSettings,
    bool StaffPick,
    bool Moderate,
    bool ManageRights,
    bool ManageBotsAndPets,
    bool KickAll,
    bool MuteRoom,
    bool Unload,
    bool Alert,
    bool TransferOwner,
    bool PickUpFurni
);
