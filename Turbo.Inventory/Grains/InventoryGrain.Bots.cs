using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Bots.Snapshots;
using Turbo.Primitives.Messages.Outgoing.Inventory.Bots;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Inventory.Grains;

internal sealed partial class InventoryGrain
{
    public Task<ImmutableArray<BotSnapshot>> GetAllBotSnapshotsAsync(CancellationToken ct) =>
        BotModule.GetAllAsync(ct);

    public Task<BotSnapshot?> GetBotSnapshotAsync(int botId, CancellationToken ct) =>
        BotModule.GetAsync(botId, ct);

    public Task<BotSnapshot?> TryCheckOutBotAsync(int botId, RoomId roomId, CancellationToken ct) =>
        BotModule.TryCheckOutAsync(botId, roomId, ct);

    public Task<bool> ReturnBotAsync(BotSnapshot snapshot, CancellationToken ct) =>
        BotModule.ReturnAsync(snapshot, ct);

    public Task<BotSnapshot?> CreateBotAsync(
        string name,
        string motto,
        string figure,
        AvatarGenderType gender,
        CancellationToken ct
    ) => BotModule.CreateAsync(name, motto, figure, gender, ct);

    public Task<bool> DeleteBotAsync(int botId, CancellationToken ct) =>
        BotModule.DeleteAsync(botId, ct);

    public async Task SendBotInventoryAsync(CancellationToken ct) =>
        await _grainFactory.SendComposerToPlayerAsync(
            PlayerId,
            new BotInventoryEventMessageComposer { Bots = await GetAllBotSnapshotsAsync(ct) },
            ct
        );
}
