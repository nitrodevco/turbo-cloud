using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User.Smart;

/// <summary><c>~pet.max_energy</c> (Wired Faculty, 03/2026): The most energy a pet can have. Read only.</summary>
public sealed class PetMaxEnergyVariable(RoomGrain roomGrain) : PetStatSmartVariable(roomGrain)
{
    protected override string VariableName => "~pet.max_energy";

    protected override WiredVariableValue GetValueForAvatar(IRoomPet pet) =>
        _roomGrain._petConfig.MaxEnergy;
}
