using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Players.Snapshots.Wardrobe;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Players.Grains.Wardrobe;

public interface IPlayerWardrobeGrain : IGrainWithIntegerKey
{
    public Task<List<OutfitDataSnapshot>> GetOutfitsAsync(CancellationToken ct);
    public Task<bool> SaveOutfitAsync(
        int slotId,
        string figure,
        AvatarGenderType gender,
        CancellationToken ct
    );
}
