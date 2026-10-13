namespace Turbo.Rooms.Wired.Variables;

/// <summary>
/// A smart variable (<c>~name</c>): one a kind of furni or pet brings along. Habbo lists every one
/// in every room and marks it invisible while nothing of its kind is there (hhus,
/// WIN63-202610121709: ~pet.*, ~plant.*, ~chest.* came invisible in a room without them, ~clock.*
/// visible beside its clock). The built-in @pet_owner_id follows the same rule for pets.
/// </summary>
public interface IWiredSmartVariable
{
    bool IsPresent();

    /// <summary>Describes the variable again, after something of its kind came or went.</summary>
    void RefreshPresence();
}
