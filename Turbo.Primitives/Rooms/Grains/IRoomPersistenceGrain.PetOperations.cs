using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Pets.Snapshots;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Rooms.Grains;

public partial interface IRoomPersistenceGrain
{
    /// <summary>Writes a supplier-attributed nutrition gain and its fact exactly once.</summary>
    public Task<PetNutritionOperationResult> ApplyPetNutritionOperationAsync(
        string operationId,
        int petId,
        PlayerId ownerId,
        PlayerId supplierId,
        int baseNutrition,
        int requestedNutrition,
        int maxNutrition,
        CancellationToken ct
    );

    /// <summary>Reserves the actor's pet scratch and atomically applies the pet and fact side.</summary>
    public Task<PetRespectOperationResult> ApplyPetRespectOperationAsync(
        string operationId,
        PlayerId actorId,
        int petId,
        PlayerId ownerId,
        int baseRespect,
        CancellationToken ct
    );
}
