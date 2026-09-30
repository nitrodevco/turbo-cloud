using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Messages.Outgoing.Handshake;
using Turbo.Primitives.Messages.Outgoing.Perk;
using Turbo.Primitives.Messages.Outgoing.Turbo;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Events;
using Turbo.Primitives.Players.Snapshots.Permissions;

namespace Turbo.Players.Grains.Permissions;

internal sealed partial class PlayerPermissionGrain
{
    public Task SendClientStateAsync(CancellationToken ct)
    {
        EnsureResolved();

        return SendClientStateCoreAsync(rights: true, nodes: true, ct);
    }

    public Task SendPermissionNodesAsync(CancellationToken ct)
    {
        EnsureResolved();

        return SendClientStateCoreAsync(rights: false, nodes: true, ct);
    }

    /// <summary>
    /// Tells whoever needs it about a change: the client when the projection moved, plugins when
    /// a node or meta value moved, and the room the player stands in when either moved. <paramref name="force"/> tells both even when
    /// nothing looks different, for rows swept after an activation that may have run out while the
    /// grain was collected and its player online.
    /// </summary>
    private async Task PublishChangesAsync(bool force, CancellationToken ct)
    {
        var client = _state.Client!;
        var sent = force ? null : _state.SentClient;
        var rights = sent is null || !client.RightsMatch(sent);
        var nodes = sent is null || !client.NodesMatch(sent);

        if (rights || nodes)
            await SendClientStateCoreAsync(rights, nodes, ct);

        var resolved = _state.Resolved!;

        Announce(resolved);

        if (!force && _state.SentRoom is { } room && room.RoomCopyMatches(resolved))
            return;

        await _grainFactory
            .GetPlayerPresenceGrain(PlayerId)
            .OnPermissionsChangedAsync(resolved, ct);

        _state.SentRoom = resolved;
    }

    /// <summary>
    /// Raises <see cref="PlayerPermissionsChangedEvent"/> if what the player holds differs from
    /// what was last announced. Not awaited: a handler may call back into this grain, which would
    /// wait on the call raising it.
    /// </summary>
    private void Announce(ResolvedPermissionsSnapshot resolved)
    {
        if (_state.Announced is not { } previous || previous.HoldsSame(resolved))
            return;

        _state.Announced = resolved;

        _eventSystem
            .PublishAsync(
                new PlayerPermissionsChangedEvent
                {
                    PlayerId = PlayerId,
                    Previous = previous,
                    Current = resolved,
                },
                CancellationToken.None
            )
            .LogAndForget(_logger, "announce the permissions of player {PlayerId}", PlayerId);
    }

    /// <summary>
    /// Tells the player's session what changed: <paramref name="rights"/> sends their security
    /// level, ambassador flag and perks, <paramref name="nodes"/> their client-facing nodes, which
    /// the presence passes on only to a session that accepted <c>permission.nodes</c>. The club
    /// level shares the <c>UserRights</c> packet, so it is read from the subscription grain, which
    /// never awaits this one. Sent to an offline player it goes nowhere.
    /// </summary>
    private async Task SendClientStateCoreAsync(bool rights, bool nodes, CancellationToken ct)
    {
        var client = _state.Client!;
        var composers = new List<IComposer>(3);

        if (rights)
        {
            var hasClub = await _grainFactory.HasActiveClubAsync(PlayerId, ct);

            composers.Add(
                new UserRightsMessage
                {
                    ClubLevel = hasClub ? ClubLevelType.Vip : ClubLevelType.None,
                    SecurityLevel = client.SecurityLevel,
                    IsAmbassador = client.IsAmbassador,
                }
            );
            composers.Add(
                new PerkAllowancesMessageComposer
                {
                    Perks =
                    [
                        .. client.Perks.Select(x => new PerkAllowanceItem
                        {
                            Code = PlayerPerkExtensions.ToLegacyString(x.Perk),
                            IsAllowed = x.IsAllowed,
                            ErrorMessage = x.Refusal,
                        }),
                    ],
                }
            );
        }

        // After UserRights, so a client falling back to the level never holds nodes that
        // disagree with a level it has not been sent yet.
        if (nodes)
            composers.Add(new TurboPermissionNodesMessage { Nodes = client.Nodes });

        await _grainFactory.GetPlayerPresenceGrain(PlayerId).SendComposerAsync(composers, ct);

        // What the client knows is the previous state with the parts just sent replaced.
        _state.SentClient = (rights, nodes, _state.SentClient) switch
        {
            (true, true, _) or (_, _, null) => client,
            (true, false, { } sent) => client with { Nodes = sent.Nodes },
            (false, true, { } sent) => sent with { Nodes = client.Nodes },
            _ => _state.SentClient,
        };

        _logger.LogDebug(
            "Sent permissions to player {PlayerId}: security level {SecurityLevel}, ambassador {IsAmbassador}",
            PlayerId,
            client.SecurityLevel,
            client.IsAmbassador
        );
    }
}
