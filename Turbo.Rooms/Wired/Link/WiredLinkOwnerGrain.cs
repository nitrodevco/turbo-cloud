using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Database.Context;
using Turbo.Database.Entities.WiredLink;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Wired.Link;
using Turbo.Rooms.Configuration;

namespace Turbo.Rooms.Wired.Link;

/// <summary>
/// One player's WIRED Links networks and Link configurations (see <see cref="IWiredLinkOwnerGrain"/>).
/// Every change writes through to the database before it is applied here, so there is nothing to
/// flush when the grain goes away.
/// </summary>
internal sealed class WiredLinkOwnerGrain : Grain, IWiredLinkOwnerGrain
{
    /// <summary>The longest network name the window lets through (AS3 <c>WiredLinkController.validName</c>).</summary>
    public const int NAME_MAX_LENGTH = 255;

    private const string NO_NETWORK = "0";

    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory;
    private readonly WiredConfig _config;
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<WiredLinkOwnerGrain> _logger;

    private readonly WiredLinkOwnerLiveState _state;

    public WiredLinkOwnerGrain(
        IDbContextFactory<TurboDbContext> dbCtxFactory,
        IOptions<WiredConfig> wiredConfig,
        IGrainFactory grainFactory,
        ILogger<WiredLinkOwnerGrain> logger
    )
    {
        _dbCtxFactory = dbCtxFactory;
        _config = wiredConfig.Value;
        _grainFactory = grainFactory;
        _logger = logger;

        _state = new() { PlayerId = this.GetPlayerId() };
    }

    private PlayerId OwnerId => _state.PlayerId;

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        var ownerId = OwnerId.Value;

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        foreach (
            var network in await dbCtx
                .WiredLinkNetworks.AsNoTracking()
                .Where(x => x.PlayerEntityId == ownerId)
                .ToListAsync(ct)
        )
            _state.Networks[network.Id] = network.Name;

        foreach (
            var link in await dbCtx
                .WiredLinkEndpoints.AsNoTracking()
                .Where(x => x.PlayerEntityId == ownerId)
                .ToListAsync(ct)
        )
        {
            _state.Links[new WiredLinkEndpoint(link.RoomEntityId, link.FurniId)] =
                new WiredLinkOwnerLiveState.LinkState
                {
                    NetworkId = link.NetworkEntityId,
                    ReceiveLocal = link.ReceiveLocal,
                    IsBuildersClub = link.IsBuildersClub,
                };
        }
    }

    public Task<WiredLinkConfigurationSnapshot> GetConfigurationAsync(
        WiredLinkEndpoint link,
        CancellationToken ct
    )
    {
        _state.Links.TryGetValue(link, out var state);

        return Task.FromResult(
            new WiredLinkConfigurationSnapshot
            {
                SelectedNetworkId = Format(state?.NetworkId),
                ReceiveLocal = state?.ReceiveLocal ?? false,
                Networks = Networks(link.RoomId, _state.Networks.Keys),
            }
        );
    }

    public async Task<WiredLinkOperationOutcome> SaveAsync(
        WiredLinkEndpoint link,
        bool isBuildersClub,
        string networkId,
        bool receiveLocal,
        CancellationToken ct
    )
    {
        int? network = null;

        if (networkId != NO_NETWORK)
        {
            if (ParseNetwork(networkId, out var parsed) is { } failure)
                return WiredLinkOperationOutcome.Failed(failure);

            // One Link per network in a room (wiredlink.error.5).
            if (
                _state.Links.Any(x =>
                    x.Key.RoomId == link.RoomId && x.Key != link && x.Value.NetworkId == parsed
                )
            )
                return WiredLinkOperationOutcome.Failed(
                    WiredLinkResultCodes.NETWORK_IN_USE_IN_ROOM
                );

            network = parsed;
        }

        await using (var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct))
        {
            var row = await dbCtx.WiredLinkEndpoints.FirstOrDefaultAsync(
                x => x.RoomEntityId == link.RoomId.Value && x.FurniId == link.FurniId,
                ct
            );

            if (network is null && !receiveLocal)
            {
                // A Link that neither sends nor receives across rooms keeps nothing.
                if (row is not null)
                    dbCtx.WiredLinkEndpoints.Remove(row);
            }
            else
            {
                row ??= dbCtx
                    .WiredLinkEndpoints.Add(
                        new WiredLinkEndpointEntity
                        {
                            RoomEntityId = link.RoomId.Value,
                            FurniId = link.FurniId,
                            PlayerEntityId = OwnerId.Value,
                        }
                    )
                    .Entity;

                row.PlayerEntityId = OwnerId.Value;
                row.NetworkEntityId = network;
                row.ReceiveLocal = receiveLocal;
                row.IsBuildersClub = isBuildersClub;
            }

            await dbCtx.SaveChangesAsync(ct);
        }

        var previous = _state.Links.TryGetValue(link, out var before) ? before.NetworkId : null;

        if (network is null && !receiveLocal)
            _state.Links.Remove(link);
        else
            _state.Links[link] = new WiredLinkOwnerLiveState.LinkState
            {
                NetworkId = network,
                ReceiveLocal = receiveLocal,
                IsBuildersClub = isBuildersClub,
            };

        // The networks whose counts changed: the one left and the one joined (recorded: a switch
        // answers both, a save without a network neither).
        return Succeeded(link, [.. new[] { previous, network }.OfType<int>().Distinct()]);
    }

    public async Task<WiredLinkOperationOutcome> CreateNetworkAsync(
        WiredLinkEndpoint link,
        string name,
        CancellationToken ct
    )
    {
        if (CheckName(name, null) is { } failure)
            return WiredLinkOperationOutcome.Failed(failure);

        if (_config.LinkNetworkLimit > 0 && _state.Networks.Count >= _config.LinkNetworkLimit)
            return WiredLinkOperationOutcome.Failed(WiredLinkResultCodes.NETWORK_LIMIT);

        await using (var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct))
        {
            var row = new WiredLinkNetworkEntity { PlayerEntityId = OwnerId.Value, Name = name };

            dbCtx.WiredLinkNetworks.Add(row);

            await dbCtx.SaveChangesAsync(ct);

            _state.Networks[row.Id] = name;

            return Succeeded(link, [row.Id]);
        }
    }

    public async Task<WiredLinkOperationOutcome> RenameNetworkAsync(
        WiredLinkEndpoint link,
        string networkId,
        string name,
        CancellationToken ct
    )
    {
        if (ParseNetwork(networkId, out var network) is { } failure)
            return WiredLinkOperationOutcome.Failed(failure);

        if (CheckName(name, network) is { } nameFailure)
            return WiredLinkOperationOutcome.Failed(nameFailure);

        await using (var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct))
        {
            await dbCtx
                .WiredLinkNetworks.Where(x => x.Id == network)
                .ExecuteUpdateAsync(x => x.SetProperty(n => n.Name, name), ct);
        }

        _state.Networks[network] = name;

        return Succeeded(link, [network]);
    }

    public async Task<WiredLinkOperationOutcome> DeleteNetworkAsync(
        WiredLinkEndpoint link,
        string networkId,
        CancellationToken ct
    )
    {
        if (ParseNetwork(networkId, out var network) is { } failure)
            return WiredLinkOperationOutcome.Failed(failure);

        var connected = _state.Links.Where(x => x.Value.NetworkId == network).ToList();

        await using (var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct))
        {
            // Its Links lose the connection; one that does not receive its own room keeps nothing.
            await dbCtx
                .WiredLinkEndpoints.Where(x => x.NetworkEntityId == network && !x.ReceiveLocal)
                .ExecuteDeleteAsync(ct);
            await dbCtx
                .WiredLinkEndpoints.Where(x => x.NetworkEntityId == network)
                .ExecuteUpdateAsync(x => x.SetProperty(e => e.NetworkEntityId, (int?)null), ct);
            await dbCtx.WiredLinkNetworks.Where(x => x.Id == network).ExecuteDeleteAsync(ct);
        }

        _state.Networks.Remove(network);

        foreach (var (endpoint, state) in connected)
        {
            if (state.ReceiveLocal)
                state.NetworkId = null;
            else
                _state.Links.Remove(endpoint);
        }

        // The asking Link answers with its own configuration; the others the delete disconnected
        // are told apart (recorded: WiredLinkConfigurationInvalidated 4 ms later).
        return Succeeded(link, []) with
        {
            RemovedNetworkId = Format(network),
            Disconnected = [.. connected.Select(x => x.Key).Where(x => x != link)],
        };
    }

    public async Task<ImmutableArray<WiredLinkConnectionSnapshot>?> GetConnectionsAsync(
        string networkId,
        CancellationToken ct
    )
    {
        if (ParseNetwork(networkId, out var network) is not null)
            return null;

        var connected = _state
            .Links.Where(x => x.Value.NetworkId == network)
            .OrderBy(x => x.Key.RoomId.Value)
            .ThenBy(x => x.Key.FurniId)
            .ToList();
        var roomIds = connected.Select(x => x.Key.RoomId.Value).Distinct().ToList();

        Dictionary<int, string> roomNames;

        await using (var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct))
        {
            roomNames = await dbCtx
                .Rooms.AsNoTracking()
                .Where(x => roomIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        }

        return
        [
            .. connected.Select(x => new WiredLinkConnectionSnapshot
            {
                RoomId = x.Key.RoomId.Value,
                RoomName = roomNames.GetValueOrDefault(x.Key.RoomId.Value, string.Empty),
                FurniId = x.Key.FurniId,
                IsBuildersClub = x.Value.IsBuildersClub,
                ReceiveLocal = x.Value.ReceiveLocal,
            }),
        ];
    }

    public async Task DisconnectAsync(WiredLinkEndpoint link, CancellationToken ct)
    {
        if (!_state.Links.Remove(link))
            return;

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        await dbCtx
            .WiredLinkEndpoints.Where(x =>
                x.RoomEntityId == link.RoomId.Value && x.FurniId == link.FurniId
            )
            .ExecuteDeleteAsync(ct);
    }

    public async Task<int> SendAsync(
        RoomId roomId,
        ImmutableArray<int> linkIds,
        WiredLinkPayload payload,
        CancellationToken ct
    )
    {
        var targets = Route(roomId, linkIds);

        if (targets.Count == 0)
            return 0;

        HashSet<RoomId>? loaded = null;

        if (!_config.LinkDeliversToUnloadedRooms)
            loaded = [.. await _grainFactory.GetRoomDirectoryGrain().GetActiveRoomIdsAsync(ct)];

        var delivered = 0;

        foreach (var room in targets.GroupBy(x => x.RoomId))
        {
            if (loaded is not null && !loaded.Contains(room.Key))
                continue;

            ImmutableArray<int> receivers = [.. room.Select(x => x.FurniId)];

            delivered += receivers.Length;

            // Not awaited: a receiving room may itself be sending through a Link right now.
            _grainFactory
                .GetRoomGrain(room.Key)
                .ReceiveWiredLinkPayloadAsync(receivers, payload, CancellationToken.None)
                .LogAndForget(_logger, "deliver a WIRED Link payload to room " + room.Key);
        }

        return delivered;
    }

    /// <summary>
    /// The Links a send through <paramref name="linkIds"/> reaches: every other room's Link on the
    /// same network, and the sending Link itself when it receives its own room's sends
    /// ("Receive messages sent by this room"; with no network it receives only those). A room
    /// holds one Link per network, so the sending Link is the only one of its room on it.
    /// </summary>
    private List<WiredLinkEndpoint> Route(RoomId roomId, ImmutableArray<int> linkIds)
    {
        var targets = new HashSet<WiredLinkEndpoint>();

        foreach (var linkId in linkIds)
        {
            var sender = new WiredLinkEndpoint(roomId, linkId);

            if (!_state.Links.TryGetValue(sender, out var state))
                continue;

            if (state.ReceiveLocal)
                targets.Add(sender);

            if (state.NetworkId is not { } network)
                continue;

            foreach (var (endpoint, other) in _state.Links)
            {
                if (endpoint.RoomId != roomId && other.NetworkId == network)
                    targets.Add(endpoint);
            }
        }

        return [.. targets];
    }

    /// <summary>
    /// A request went through: the Link's configuration as it is now (every success carries it,
    /// recorded) and the networks it changed.
    /// </summary>
    private WiredLinkOperationOutcome Succeeded(WiredLinkEndpoint link, IEnumerable<int> changed)
    {
        _state.Links.TryGetValue(link, out var state);

        return new WiredLinkOperationOutcome
        {
            ResultCode = WiredLinkResultCodes.OK,
            SelectedNetworkId = Format(state?.NetworkId),
            ReceiveLocal = state?.ReceiveLocal ?? false,
            Networks = Networks(link.RoomId, changed),
        };
    }

    /// <summary>
    /// The networks <paramref name="ids"/>, by name in ordinal order (recorded: "LabMax 1",
    /// "LabMax 10", "LabMax 100"), with their Link counts as the room sees them.
    /// </summary>
    private ImmutableArray<WiredLinkNetworkSnapshot> Networks(
        RoomId roomId,
        IEnumerable<int> ids
    ) =>
        [
            .. ids.Where(_state.Networks.ContainsKey)
                .Select(id => (Id: id, Name: _state.Networks[id]))
                .OrderBy(x => x.Name, StringComparer.Ordinal)
                .ThenBy(x => x.Id)
                .Select(x => new WiredLinkNetworkSnapshot
                {
                    NetworkId = Format(x.Id),
                    Name = x.Name,
                    ConnectionCount = _state.Links.Values.Count(l => l.NetworkId == x.Id),
                    LinkedInCurrentRoom = _state.Links.Any(l =>
                        l.Key.RoomId == roomId && l.Value.NetworkId == x.Id
                    ),
                }),
        ];

    /// <summary>Null when <paramref name="networkId"/> names one of the owner's networks, else why not.</summary>
    private int? ParseNetwork(string networkId, out int network)
    {
        if (
            !int.TryParse(networkId, NumberStyles.None, CultureInfo.InvariantCulture, out network)
            || network <= 0
        )
            return WiredLinkResultCodes.INVALID_NETWORK_ID;

        return _state.Networks.ContainsKey(network) ? null : WiredLinkResultCodes.NETWORK_NOT_FOUND;
    }

    /// <summary>
    /// Null when <paramref name="name"/> may name a network: some text, at most 255 characters
    /// (AS3 <c>validName</c>), and none of the owner's other networks already called exactly that
    /// (<c>wiredlink.error.4</c>). Names are kept as typed: recorded, "labnet a" and "LabNet A"
    /// are both allowed and " LabNet A " keeps its spaces.
    /// </summary>
    private int? CheckName(string name, int? renaming)
    {
        if (name.Length > NAME_MAX_LENGTH || string.IsNullOrWhiteSpace(name))
            return WiredLinkResultCodes.INVALID_NAME;

        return _state.Networks.Any(x =>
            x.Key != renaming && string.Equals(x.Value, name, StringComparison.Ordinal)
        )
            ? WiredLinkResultCodes.DUPLICATE_NAME
            : null;
    }

    private static string Format(int? network) =>
        network is { } id ? id.ToString(CultureInfo.InvariantCulture) : NO_NETWORK;
}
