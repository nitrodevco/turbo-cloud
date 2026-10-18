using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Bots.Snapshots;
using Turbo.Primitives.Messages.Outgoing.Inventory.Bots;
using Turbo.Primitives.Movements;
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

    public Task<BotSnapshot?> TryCheckOutBotAsync(
        int botId,
        RoomId roomId,
        MovementCause cause,
        CancellationToken ct
    ) => BotModule.TryCheckOutAsync(botId, roomId, cause, ct);

    public Task<bool> ReturnBotAsync(
        BotSnapshot snapshot,
        MovementCause cause,
        CancellationToken ct
    ) => BotModule.ReturnAsync(snapshot, cause, ct);

    public Task<BotSnapshot?> CreateBotAsync(
        string name,
        string motto,
        string figure,
        AvatarGenderType gender,
        MovementCause cause,
        CancellationToken ct
    ) => BotModule.CreateAsync(name, motto, figure, gender, cause, ct);

    public Task<bool> DeleteBotAsync(int botId, MovementCause cause, CancellationToken ct) =>
        BotModule.DeleteAsync(botId, cause, ct);

    public async Task SendBotInventoryAsync(CancellationToken ct) =>
        await _grainFactory.SendComposerToPlayerAsync(
            PlayerId,
            new BotInventoryEventMessageComposer { Bots = await GetAllBotSnapshotsAsync(ct) },
            ct
        );
}
