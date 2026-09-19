namespace Turbo.Primitives.Pets.Enums;

/// <summary>
/// First field of <c>PetBreeding</c>. The client opens the confirmation for a request
/// (<see cref="Requested"/> for the invited owner, <see cref="RequestedByMe"/> for the one who
/// asked) and closes it on a cancel or accept.
/// </summary>
public enum PetBreedingState
{
    Requested = 0,
    Cancelled = 1,
    Accepted = 2,
    RequestedByMe = 3,
}
