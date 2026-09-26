using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;

namespace Turbo.Guilds.Grains;

/// <summary>
/// What the rest of the hotel has to be told when a group changes, published by the group
/// itself rather than by whoever asked it to change.
///
/// Every one of these goes out with <c>LogAndForget</c>, and that is the whole reason they can
/// live here at all. The room answers a controller-level check by asking this grain, so a call
/// this grain <em>awaited</em> into a room could find the room waiting on it — the deadlock this
/// file exists to avoid. A call that is not awaited holds no turn: this grain finishes, and the
/// room's question is answered on the next one.
///
/// It is the same rule <c>AGENTS.md</c> states for <c>RoomTradeGrain</c>, which awaits the room
/// and so is only ever told things. Before adding a call out of this grain, check whether the
/// callee awaits a group — and if it does, tell it, do not ask it.
/// </summary>
internal sealed partial class GuildGrain
{
    /// <summary>
    /// The group's own details moved: its name, badge, colours, type or rights level. The
    /// homeroom re-reads the group, because its listing draws the name and badge from a copy it
    /// holds and its rights come from the group's settings.
    /// </summary>
    private void NotifyHomeroomGuildChanged()
    {
        if (_state.Guild is not { } guild || guild.RoomId <= 0)
            return;

        _grainFactory
            .GetRoomGrain(guild.RoomId)
            .OnGuildChangedAsync(CancellationToken.None)
            .LogAndForget(_logger, $"refresh the homeroom of group {GuildId.Value}");
    }

    /// <summary>
    /// One member's standing moved. Only their own rights in the homeroom change, so only they
    /// are refreshed — somebody standing in it would otherwise keep what they walked in with.
    /// </summary>
    private void NotifyHomeroomMemberChanged(PlayerId playerId)
    {
        if (_state.Guild is not { } guild || guild.RoomId <= 0)
            return;

        _grainFactory
            .GetRoomGrain(guild.RoomId)
            .RefreshGuildMemberAsync(playerId, CancellationToken.None)
            .LogAndForget(
                _logger,
                $"refresh member {playerId.Value} in the homeroom of group {GuildId.Value}"
            );
    }

    /// <summary>
    /// The group's badge or colours changed, so its furni has to be repainted wherever it
    /// stands. Only loaded rooms are told: the furni holds nothing but the group id and reads
    /// the rest when it attaches, so a room that is not loaded cannot be holding the old look.
    /// </summary>
    private void NotifyGuildFurniChanged() =>
        RepaintGuildFurniAsync()
            .LogAndForget(_logger, $"repaint the furni of group {GuildId.Value}");

    private async Task RepaintGuildFurniAsync()
    {
        var guildId = GuildId;

        // The room directory is safe to await: nothing it does leads back to a group.
        var roomIds = await _grainFactory
            .GetRoomDirectoryGrain()
            .GetActiveRoomIdsAsync(CancellationToken.None);

        foreach (var roomId in roomIds)
        {
            _grainFactory
                .GetRoomGrain(roomId)
                .RefreshGuildFurniAsync(guildId, CancellationToken.None)
                .LogAndForget(_logger, $"repaint group {guildId.Value} furni in room {roomId}");
        }
    }
}
