using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Bots.Snapshots;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Inventory.Grains;

public partial interface IInventoryGrain
{
    public Task<ImmutableArray<BotSnapshot>> GetAllBotSnapshotsAsync(CancellationToken ct);
    public Task<BotSnapshot?> GetBotSnapshotAsync(int botId, CancellationToken ct);

    /// <summary>
    /// Hands a bot over to a room: the row is marked as standing in it and the bot leaves the
    /// inventory list. Null when the bot is not in this inventory.
    /// </summary>
    public Task<BotSnapshot?> TryCheckOutBotAsync(int botId, RoomId roomId, CancellationToken ct);

    /// <summary>Takes a bot back from a room with whatever its owner changed there.</summary>
    public Task<bool> ReturnBotAsync(BotSnapshot snapshot, CancellationToken ct);
    public Task<BotSnapshot?> CreateBotAsync(
        string name,
        string motto,
        string figure,
        AvatarGenderType gender,
        CancellationToken ct
    );
    public Task<bool> DeleteBotAsync(int botId, CancellationToken ct);
}
