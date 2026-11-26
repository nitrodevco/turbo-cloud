using Orleans;
using Turbo.Primitives.Rooms.Events.Avatar;

namespace Turbo.Primitives.Rooms.Events.Pet;

/// <summary>
/// A pet was taken out of the room: picked up, returned to its owner, or gone with the room.
/// The pet is already gone, so only the avatar it was is known; whatever is kept by avatar
/// (the wired user variables) lets go of it on this.
/// </summary>
[GenerateSerializer]
public sealed record PetRemovedEvent : AvatarEvent;
