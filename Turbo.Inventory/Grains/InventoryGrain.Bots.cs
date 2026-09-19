using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Bots.Snapshots;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Inventory.Grains;

internal sealed partial class InventoryGrain
{
    public Task<ImmutableArray<BotSnapshot>> GetAllBotSnapshotsAsync(CancellationToken ct) =>
        _botModule.GetAllAsync(ct);

    public Task<BotSnapshot?> GetBotSnapshotAsync(int botId, CancellationToken ct) =>
        _botModule.GetAsync(botId, ct);

    public Task<BotSnapshot?> TryCheckOutBotAsync(int botId, RoomId roomId, CancellationToken ct) =>
        _botModule.TryCheckOutAsync(botId, roomId, ct);

    public Task<bool> ReturnBotAsync(BotSnapshot snapshot, CancellationToken ct) =>
        _botModule.ReturnAsync(snapshot, ct);

    public Task<BotSnapshot?> CreateBotAsync(
        string name,
        string motto,
        string figure,
        AvatarGenderType gender,
        CancellationToken ct
    ) => _botModule.CreateAsync(name, motto, figure, gender, ct);

    public Task<bool> DeleteBotAsync(int botId, CancellationToken ct) =>
        _botModule.DeleteAsync(botId, ct);
}
