using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User.Smart;

/// <summary>
/// A smart user variable (<c>~pet.*</c>, <c>~horse.*</c>, <c>~plant.*</c>): held by every pet
/// <see cref="AppliesTo"/> accepts, and listed only while one of them is in the room. Listed
/// after the internal user variables, as the furni smart variables are after theirs.
/// </summary>
public abstract class PetSmartVariable(RoomGrain roomGrain)
    : UserValueVariable<IRoomPet>(roomGrain),
        IWiredSmartVariable
{
    protected override WiredVariableType VariableType => WiredVariableType.Smart;

    protected override WiredVariableGroupSubBandType SubBandType =>
        WiredVariableGroupSubBandType.Smart;

    protected override ushort Order => 0;

    protected override WiredVariableFlags Flags => WiredVariableFlags.HasValue;

    /// <summary>Which pets hold the variable.</summary>
    protected abstract bool AppliesTo(IRoomPet pet);

    public bool IsPresent() => PetModule.Pets.Any(AppliesTo);

    protected override bool TryGetAvatarForKey(
        in WiredVariableKey key,
        [NotNullWhen(true)] out IRoomPet? avatar
    ) => base.TryGetAvatarForKey(key, out avatar) && AppliesTo(avatar);
}

/// <summary>A <c>~pet.*</c> variable: every pet but a monsterplant, which has <c>~plant.*</c> instead.</summary>
public abstract class PetStatSmartVariable(RoomGrain roomGrain) : PetSmartVariable(roomGrain)
{
    protected override bool AppliesTo(IRoomPet pet) => !pet.IsMonsterplant;
}

/// <summary>A <c>~horse.*</c> variable: horses only.</summary>
public abstract class HorseSmartVariable(RoomGrain roomGrain) : PetSmartVariable(roomGrain)
{
    protected override bool AppliesTo(IRoomPet pet) =>
        pet.TypeId == Turbo.Primitives.Pets.PetTypes.HORSE;
}

/// <summary>A <c>~plant.*</c> variable: monsterplants only.</summary>
public abstract class PlantSmartVariable(RoomGrain roomGrain) : PetSmartVariable(roomGrain)
{
    protected override bool AppliesTo(IRoomPet pet) => pet.IsMonsterplant;
}
