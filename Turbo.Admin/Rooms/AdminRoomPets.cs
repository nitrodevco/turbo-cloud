using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Orleans;
using Turbo.Admin.Api.Contracts;
using Turbo.Database.Context;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Pets;
using Turbo.Primitives.Pets.Snapshots;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Grains;

namespace Turbo.Admin.Rooms;

/// <summary>
/// The pets standing in a room, for its page. A pet in a room is the room's: it is set, or taken
/// back to its owner's inventory, through the room grain, so those in the room and its owner see
/// the change and the room refuses a pet that is not standing in it.
/// </summary>
public sealed class AdminRoomPets(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IGrainFactory grainFactory
)
{
    /// <summary>
    /// The pets standing in the room, by name, as last written: a loaded room writes its pets'
    /// energy and nutrition now and then, not as they fall.
    /// </summary>
    public async Task<List<RoomPetItem>> ListAsync(int roomId, CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        return await dbCtx
            .Pets.AsNoTracking()
            .Where(x => x.RoomEntityId == roomId)
            .OrderBy(x => x.Name)
            .ThenBy(x => x.Id)
            .Select(x => new RoomPetItem(
                x.Id,
                x.Name,
                x.TypeId,
                x.PaletteId,
                x.Color,
                x.TypeId == PetTypes.MONSTERPLANT,
                x.PlayerEntityId,
                x.PlayerEntity!.Name,
                x.Level,
                x.Energy,
                x.Nutrition,
                x.AnyoneCanRide,
                x.HasBreedingPermission
            ))
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Sets the values given on a pet standing in the room; false when it doesn't stand there, or
    /// its name is too short or too long.
    /// </summary>
    public Task<bool> UpdateAsync(
        int roomId,
        int petId,
        RoomPetRequest request,
        CancellationToken ct
    ) =>
        Room(roomId)
            .StaffUpdatePetAsync(
                petId,
                new PetStaffEditSnapshot
                {
                    Name = request.Name,
                    Level = request.Level,
                    Energy = request.Energy,
                    Nutrition = request.Nutrition,
                    AnyoneCanRide = request.AnyoneCanRide,
                    HasBreedingPermission = request.HasBreedingPermission,
                },
                ct
            );

    /// <summary>Takes a pet out of the room, back to its owner's inventory; false when it doesn't stand there.</summary>
    public Task<bool> PickupAsync(int roomId, int petId, CancellationToken ct) =>
        Room(roomId).StaffPickupPetAsync(petId, ct);

    private IRoomGrain Room(int roomId) => grainFactory.GetRoomGrain(new RoomId(roomId));
}
