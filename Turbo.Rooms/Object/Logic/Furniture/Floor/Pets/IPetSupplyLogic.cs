using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Pets.Enums;
using Turbo.Primitives.Rooms.Object.Avatars;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Pets;

/// <summary>A floor item pets walk to on their own or on command: food, drink, a toy, a basket.</summary>
public interface IPetSupplyLogic
{
    public PetSupplyType SupplyType { get; }

    /// <summary>False once a consumable bowl or bottle is empty.</summary>
    public bool HasSuppliesLeft { get; }
    public Task OnPetReachedAsync(IRoomPet pet, CancellationToken ct);
}
