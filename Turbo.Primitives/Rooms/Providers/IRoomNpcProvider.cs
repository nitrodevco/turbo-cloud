using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Bots.Snapshots;
using Turbo.Primitives.Pets.Snapshots;

namespace Turbo.Primitives.Rooms.Providers;

/// <summary>Loads the pets and bots standing in a room when it activates.</summary>
public interface IRoomNpcProvider
{
    public Task<IReadOnlyList<PetSnapshot>> LoadPetsByRoomIdAsync(
        RoomId roomId,
        CancellationToken ct
    );
    public Task<IReadOnlyList<BotSnapshot>> LoadBotsByRoomIdAsync(
        RoomId roomId,
        CancellationToken ct
    );
}
