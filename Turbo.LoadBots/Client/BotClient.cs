using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.LoadBots.Knowledge;
using Turbo.LoadBots.Metrics;
using Turbo.LoadBots.Protocol;
using Turbo.LoadBots.Protocol.Decoders;
using Turbo.Revisions.Revision20260909;

namespace Turbo.LoadBots.Client;

/// <summary>A bot's login: the player it plays and the SSO ticket that logs it in.</summary>
public sealed record BotAccount(int PlayerId, string Name, string Ticket);

/// <summary>
/// One bot's session with the server, with the operations a player performs through the
/// client. Every operation sends the client's own message and waits for the reply that shows it
/// worked, timing it and recording a check, so playing normally is also testing.
/// </summary>
public sealed class BotClient : IAsyncDisposable
{
    private static readonly string PRODUCTION = new Revision20260909().Revision;

    private readonly BotAccount _account;
    private readonly LoadBotOptions _options;
    private readonly HotelKnowledge _knowledge;
    private readonly BotMetrics _metrics;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<int, InventoryItem> _inventory = new();
    private BotConnection? _connection;
    private RoomView? _room;
    private CatalogNode? _catalogIndex;
    private volatile bool _inventoryInvalid;

    public BotClient(
        BotAccount account,
        LoadBotOptions options,
        HotelKnowledge knowledge,
        BotMetrics metrics,
        ILogger logger
    )
    {
        _account = account;
        _options = options;
        _knowledge = knowledge;
        _metrics = metrics;
        _logger = logger;
    }

    public string Name => _account.Name;
    public int PlayerId => _account.PlayerId;
    public RoomView? Room => _room;
    public bool IsConnected => _connection?.IsOpen == true;
    public IReadOnlyCollection<InventoryItem> Inventory => [.. _inventory.Values];

    private BotConnection Connection =>
        _connection ?? throw new InvalidOperationException("The bot is not connected.");

    #region Session

    /// <summary>Connects, says hello in the revision the server speaks and logs in by ticket.</summary>
    public async Task<bool> ConnectAsync(CancellationToken ct)
    {
        IBotTransport transport = string.Equals(
            _options.Transport,
            "websocket",
            StringComparison.OrdinalIgnoreCase
        )
            ? new WebSocketBotTransport(new Uri(_options.WebSocketUrl))
            : new TcpBotTransport(_options.Host, _options.Port);

        _connection = new BotConnection(transport, _metrics, _logger);
        RegisterHandlers(_connection);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            await _connection.ConnectAsync(ct);
        }
        catch (Exception ex)
            when (ex
                    is System.Net.Sockets.SocketException
                        or System.Net.WebSockets.WebSocketException
                        or TimeoutException
            )
        {
            _metrics.Operation("session.connect", stopwatch.Elapsed, false);
            _metrics.Check("session.connects", false, CheckSeverity.Hard, $"{Name}: {ex.Message}");

            return false;
        }

        _metrics.Operation("session.connect", stopwatch.Elapsed, true);

        await Connection.SendAsync(ClientRequests.ClientHello(PRODUCTION), ct);

        var authenticated = await RequestAsync(
            "session.login",
            ClientRequests.SsoTicket(_account.Ticket, (int)stopwatch.ElapsedMilliseconds),
            m => m.Header == MessageComposer.AuthenticationOKMessageComposer,
            ct,
            TimeSpan.FromSeconds(Math.Max(15, _options.ReplyTimeoutSeconds))
        );

        if (authenticated is null)
            return false;

        var accountId = authenticated.Reader().Int();

        return _metrics.Check(
            "session.login_is_the_ticket_owner",
            accountId == PlayerId,
            CheckSeverity.Hard,
            $"{Name}: logged in as {accountId}, ticket belongs to {PlayerId}"
        );
    }

    public async Task DisconnectAsync()
    {
        if (_connection is null)
            return;

        await _connection.DisposeAsync();
        _connection = null;
        _room = null;
    }

    public ValueTask DisposeAsync() => new(DisconnectAsync());

    private void RegisterHandlers(BotConnection connection)
    {
        // Room entry: a new room's messages start with OpenConnection.
        connection.On(
            MessageComposer.OpenConnectionMessageComposer,
            m => _room = new RoomView(m.Reader().Int(), PlayerId)
        );
        connection.On(MessageComposer.CloseConnectionMessageComposer, _ => _room = null);
        connection.On(
            MessageComposer.RoomEntryInfoMessageComposer,
            m =>
            {
                var (roomId, isOwner) = RoomDecoders.RoomEntryInfo(m.Reader());

                if (_room?.RoomId == roomId)
                    _room.SetEntered(isOwner);
            }
        );
        connection.On(
            MessageComposer.HeightMapMessageComposer,
            m => _room?.SetMap(RoomDecoders.HeightMap(m.Reader()))
        );
        connection.On(
            MessageComposer.HeightMapUpdateMessageComposer,
            m => _room?.UpdateMap(RoomDecoders.HeightMapUpdate(m.Reader()))
        );
        connection.On(
            MessageComposer.FloorHeightMapMessageComposer,
            m => _room?.SetPlan(RoomDecoders.FloorHeightMap(m.Reader()))
        );
        connection.On(
            MessageComposer.UsersMessageComposer,
            m => _room?.AddAvatars(RoomDecoders.Users(m.Reader()))
        );
        connection.On(
            MessageComposer.UserRemoveMessageComposer,
            m => _room?.RemoveAvatar(RoomDecoders.UserRemove(m.Reader()))
        );
        connection.On(
            MessageComposer.UserUpdateMessageComposer,
            m => _room?.UpdateStatuses(RoomDecoders.UserUpdate(m.Reader()))
        );
        connection.On(
            MessageComposer.ObjectsMessageComposer,
            m => _room?.SetItems(RoomDecoders.Objects(m.Reader()))
        );
        connection.On(
            MessageComposer.ObjectAddMessageComposer,
            m => _room?.UpsertItem(RoomDecoders.ObjectAdd(m.Reader()))
        );
        connection.On(
            MessageComposer.ObjectUpdateMessageComposer,
            m => _room?.UpsertItem(RoomDecoders.ObjectUpdate(m.Reader()))
        );
        connection.On(
            MessageComposer.ObjectRemoveMessageComposer,
            m => _room?.RemoveItem(RoomDecoders.ObjectRemove(m.Reader()))
        );

        foreach (
            var header in new[]
            {
                MessageComposer.ChatMessageComposer,
                MessageComposer.ShoutMessageComposer,
                MessageComposer.WhisperMessageComposer,
            }
        )
            connection.On(header, m => _room?.AddChat(RoomDecoders.Chat(m.Reader())));

        // Inventory.
        connection.On(
            MessageComposer.FurniListMessageComposer,
            m =>
            {
                var fragment = InventoryDecoders.FurniList(m.Reader());

                if (fragment.FragmentIndex == 0)
                    _inventory.Clear();

                foreach (var item in fragment.Items)
                    _inventory[item.ItemId] = item;
            }
        );
        connection.On(
            MessageComposer.FurniListAddOrUpdateMessageComposer,
            m =>
            {
                foreach (var item in InventoryDecoders.FurniListAddOrUpdate(m.Reader()))
                    _inventory[item.ItemId] = item;
            }
        );
        connection.On(
            MessageComposer.FurniListRemoveMessageComposer,
            m => _inventory.TryRemove(InventoryDecoders.FurniListRemove(m.Reader()), out _)
        );
        connection.On(
            MessageComposer.FurniListInvalidateMessageComposer,
            _ => _inventoryInvalid = true
        );

        connection.On(
            MessageComposer.FloodControlMessageComposer,
            _ => _metrics.Count("chat.flood_controlled")
        );
        connection.On(
            MessageComposer.DisconnectReasonMessageComposer,
            _ => _metrics.Count("session.disconnect_reason_received")
        );
    }

    #endregion

    #region Requests

    /// <summary>
    /// Sends one request and waits for the reply <paramref name="match"/> accepts, timing it as
    /// <paramref name="operation"/>. A reply that never comes is a failed check and null.
    /// </summary>
    private async Task<ServerMessage?> RequestAsync(
        string operation,
        ClientMessage request,
        Func<ServerMessage, bool> match,
        CancellationToken ct,
        TimeSpan? timeout = null,
        CheckSeverity severity = CheckSeverity.Hard
    )
    {
        var reply = Connection.ExpectAsync(match, timeout ?? _options.ReplyTimeout, operation);
        var stopwatch = Stopwatch.StartNew();

        await Connection.SendAsync(request, ct);

        try
        {
            var message = await reply.WaitAsync(ct);
            _metrics.Operation(operation, stopwatch.Elapsed, true);
            _metrics.Check($"{operation}.answered", true, severity);

            return message;
        }
        catch (BotTimeoutException ex)
        {
            _metrics.Operation(operation, stopwatch.Elapsed, false);
            _metrics.Check($"{operation}.answered", false, severity, $"{Name}: {ex.Message}");

            return null;
        }
    }

    /// <summary>Waits for a message without sending anything; true when it came in time.</summary>
    public async Task<bool> WaitForAsync(
        Func<ServerMessage, bool> match,
        TimeSpan timeout,
        string what,
        CancellationToken ct
    )
    {
        try
        {
            await Connection.ExpectAsync(match, timeout, what).WaitAsync(ct);

            return true;
        }
        catch (BotTimeoutException)
        {
            return false;
        }
    }

    #endregion

    #region Rooms

    /// <summary>Creates a room through the navigator's "create room" flow.</summary>
    public async Task<int?> CreateRoomAsync(
        string name,
        string model,
        int maxUsers,
        CancellationToken ct
    )
    {
        var reply = await RequestAsync(
            "room.create",
            ClientRequests.CreateFlat(name, "Load test room", model, 0, maxUsers, 0),
            m => m.Header == MessageComposer.FlatCreatedMessageComposer,
            ct
        );

        if (reply is null)
            return null;

        var (roomId, createdName) = NavigatorDecoders.FlatCreated(reply.Reader());

        _metrics.Check(
            "room.create.keeps_name",
            createdName == name,
            CheckSeverity.Hard,
            $"{Name}: asked for '{name}', got '{createdName}'"
        );

        return roomId;
    }

    /// <summary>
    /// Enters a room and waits for the whole entry sequence, then checks the room arrived
    /// consistent: a map, the floor plan matching it, and the bot's own avatar in it.
    /// </summary>
    public async Task<bool> EnterRoomAsync(int roomId, CancellationToken ct)
    {
        var entered = Connection.ExpectAsync(
            m =>
                m.Header == MessageComposer.RoomEntryInfoMessageComposer
                && m.Reader().Int() == roomId,
            _options.ReplyTimeout,
            "room entry"
        );
        var refused = Connection.ExpectAsync(
            m =>
                m.Header
                    is MessageComposer.CantConnectMessageComposer
                        or MessageComposer.FlatAccessDeniedMessageComposer
                        or MessageComposer.CloseConnectionMessageComposer,
            _options.ReplyTimeout,
            "room refusal"
        );

        var stopwatch = Stopwatch.StartNew();
        await Connection.SendAsync(ClientRequests.OpenFlatConnection(roomId), ct);

        var first = await Task.WhenAny(entered, refused);

        if (first == refused && refused.IsCompletedSuccessfully)
        {
            _metrics.Operation("room.enter", stopwatch.Elapsed, false);
            _metrics.Count("room.enter.refused");

            return false;
        }

        try
        {
            await entered.WaitAsync(ct);
        }
        catch (BotTimeoutException ex)
        {
            _metrics.Operation("room.enter", stopwatch.Elapsed, false);
            _metrics.Check(
                "room.enter.answered",
                false,
                CheckSeverity.Hard,
                $"{Name}: {ex.Message}"
            );

            return false;
        }

        _metrics.Operation("room.enter", stopwatch.Elapsed, true);
        _metrics.Check("room.enter.answered", true);

        // The user list can trail the entry info by a moment; give the own avatar a short grace.
        var room = _room;
        var deadline = DateTime.UtcNow.AddSeconds(3);

        while (room is not null && room.SelfObjectId < 0 && DateTime.UtcNow < deadline)
            await Task.Delay(50, ct);

        room = _room;

        if (room is null || room.RoomId != roomId)
            return false;

        CheckRoomConsistent(room);

        // The client asks for the room's details as it opens.
        await Connection.SendAsync(ClientRequests.GetGuestRoom(roomId, false, false), ct);

        return room.SelfObjectId >= 0;
    }

    private void CheckRoomConsistent(RoomView room)
    {
        var map = room.Map;
        var plan = room.Plan;

        _metrics.Check(
            "room.enter.has_heightmap",
            map is not null,
            CheckSeverity.Hard,
            $"{Name}: room {room.RoomId}"
        );
        _metrics.Check(
            "room.enter.has_floorplan",
            plan is not null,
            CheckSeverity.Hard,
            $"{Name}: room {room.RoomId}"
        );
        _metrics.Check(
            "room.enter.sees_own_avatar",
            room.SelfObjectId >= 0,
            CheckSeverity.Hard,
            $"{Name}: room {room.RoomId}"
        );

        if (map is null || plan is null)
            return;

        var rows = RoomDecoders.ModelRows(plan.ModelData);
        var planWidth = rows.Length == 0 ? 0 : rows.Max(x => x.Length);

        _metrics.Check(
            "room.enter.heightmap_matches_floorplan",
            rows.Length == map.Length && planWidth == map.Width,
            CheckSeverity.Hard,
            $"{Name}: room {room.RoomId} plan {planWidth}x{rows.Length}, heightmap {map.Width}x{map.Length}"
        );
    }

    /// <summary>
    /// Walks to a tile and waits to stand on it. Returns whether the avatar got there; a path
    /// blocked by someone else is the room's doing, so the check is soft.
    /// </summary>
    public async Task<bool> WalkToAsync(int x, int y, CancellationToken ct)
    {
        var room = _room;

        if (room is null || room.SelfObjectId < 0)
            return false;

        var self = room.SelfObjectId;
        var start = room.Position(self) ?? (x, y);
        // Half a second a tile, as the server walks. A path can wind round furni far longer than
        // the straight line, so the bound is a walk the length of the room's edges, plus slack.
        var map = room.Map;
        var longestPath = map is null
            ? 2 * Math.Max(Math.Abs(start.X - x), Math.Abs(start.Y - y))
            : map.Width + map.Length;
        var timeout = TimeSpan.FromSeconds(3 + longestPath * 0.5);

        // How long the walk takes is mostly its length; how soon the avatar sets off is how fast
        // the room answered, so that is timed on its own, from the first step's move status.
        var setOff = Stopwatch.StartNew();
        var started = Connection.ExpectAsync(
            m =>
                m.Header == MessageComposer.UserUpdateMessageComposer
                && RoomDecoders
                    .UserUpdate(m.Reader())
                    .Any(s =>
                        s.ObjectId == self && s.Status.Contains("mv ", StringComparison.Ordinal)
                    ),
            _options.ReplyTimeout,
            "avatar.walk.start"
        );
        _ = started.ContinueWith(
            t =>
            {
                _metrics.Operation("avatar.walk.start", setOff.Elapsed, t.IsCompletedSuccessfully);
                _ = t.Exception;
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default
        );

        var arrived = await RequestAsync(
            "avatar.walk",
            ClientRequests.MoveAvatar(x, y),
            m =>
                m.Header == MessageComposer.UserUpdateMessageComposer
                && RoomDecoders
                    .UserUpdate(m.Reader())
                    .Any(s => s.ObjectId == self && s.X == x && s.Y == y),
            ct,
            timeout,
            CheckSeverity.Soft
        );

        // Someone standing on the goal stops a walk short or refuses it outright, as it should;
        // that is the room being busy, not the server failing to walk.
        if (arrived is null && room.IsTakenByOther(x, y, self))
            _metrics.Count("avatar.walk.target_taken");
        else if (arrived is null)
            _metrics.Check(
                "avatar.walk.reaches_tile",
                false,
                CheckSeverity.Soft,
                $"{Name}: room {room.RoomId} from ({start.X},{start.Y}) to ({x},{y}), stopped at {room.Position(self)}"
            );

        return arrived is not null;
    }

    /// <summary>Says a line and checks it comes back to the room under the bot's own avatar.</summary>
    public async Task<bool> SayAsync(string text, CancellationToken ct)
    {
        var room = _room;

        if (room is null || room.SelfObjectId < 0)
            return false;

        var self = room.SelfObjectId;

        var echo = await RequestAsync(
            "chat.say",
            ClientRequests.Chat(text),
            m =>
                m.Header == MessageComposer.ChatMessageComposer
                && RoomDecoders.Chat(m.Reader()) is var line
                && line.ObjectId == self
                && line.Text == text,
            ct,
            severity: CheckSeverity.Soft
        );

        return echo is not null;
    }

    public Task ShoutAsync(string text, CancellationToken ct) =>
        Connection.SendAsync(ClientRequests.Shout(text), ct);

    public Task DanceAsync(int danceId, CancellationToken ct) =>
        Connection.SendAsync(ClientRequests.Dance(danceId), ct);

    public Task SignAsync(int signId, CancellationToken ct) =>
        Connection.SendAsync(ClientRequests.Sign(signId), ct);

    public Task ExpressAsync(int expressionId, CancellationToken ct) =>
        Connection.SendAsync(ClientRequests.AvatarExpression(expressionId), ct);

    /// <summary>
    /// Waits until a chat line the bot's room shows matches; for checking what wired says.
    /// Register before acting, as with any reply.
    /// </summary>
    public Task<ServerMessage> ExpectChatAsync(
        Func<ChatLine, bool> match,
        TimeSpan timeout,
        string what
    ) =>
        Connection.ExpectAsync(
            m =>
                m.Header
                    is MessageComposer.ChatMessageComposer
                        or MessageComposer.WhisperMessageComposer
                        or MessageComposer.ShoutMessageComposer
                && match(RoomDecoders.Chat(m.Reader())),
            timeout,
            what
        );

    /// <summary>
    /// Saves a new floor plan for the room the bot owns and stands in, and checks the room
    /// comes back drawn the way it was sent.
    /// </summary>
    public async Task<bool> SaveFloorPlanAsync(
        string modelData,
        int doorX,
        int doorY,
        int doorRotation,
        CancellationToken ct
    )
    {
        var expected = RoomDecoders.ModelRows(modelData);

        var redrawn = await RequestAsync(
            "room.floorplan.save",
            ClientRequests.UpdateFloorProperties(modelData, doorX, doorY, doorRotation, 0, 0, -1),
            m =>
                m.Header == MessageComposer.FloorHeightMapMessageComposer
                && RoomDecoders
                    .ModelRows(RoomDecoders.FloorHeightMap(m.Reader()).ModelData)
                    .SequenceEqual(expected),
            ct,
            TimeSpan.FromSeconds(Math.Max(15, _options.ReplyTimeoutSeconds))
        );

        return redrawn is not null;
    }

    #endregion

    #region Navigator

    public async Task<IReadOnlyList<RoomListing>?> SearchRoomsAsync(
        string searchCode,
        string filter,
        CancellationToken ct
    )
    {
        var reply = await RequestAsync(
            "navigator.search",
            ClientRequests.NavigatorSearch(searchCode, filter),
            m =>
                m.Header == MessageComposer.NavigatorSearchResultBlocksMessageComposer
                && m.Reader().String() == searchCode,
            ct,
            severity: CheckSeverity.Soft
        );

        return reply is null ? null : NavigatorDecoders.SearchResultBlocks(reply.Reader());
    }

    #endregion

    #region Catalog and inventory

    public async Task<bool> LoadInventoryAsync(CancellationToken ct)
    {
        _inventoryInvalid = false;

        // The list comes in fragments; the last one has index total - 1.
        var reply = await RequestAsync(
            "inventory.load",
            ClientRequests.RequestFurniInventory(),
            m =>
                m.Header == MessageComposer.FurniListMessageComposer
                && m.Reader() is var r
                && r.Int() - 1 == r.Int(),
            ct
        );

        return reply is not null;
    }

    public async Task<CatalogNode?> LoadCatalogIndexAsync(CancellationToken ct)
    {
        if (_catalogIndex is not null)
            return _catalogIndex;

        var reply = await RequestAsync(
            "catalog.index",
            ClientRequests.GetCatalogIndex(),
            m => m.Header == MessageComposer.CatalogIndexMessageComposer,
            ct
        );

        _catalogIndex = reply is null ? null : CatalogDecoders.CatalogIndex(reply.Reader());

        return _catalogIndex;
    }

    public async Task<CatalogPage?> OpenCatalogPageAsync(int pageId, CancellationToken ct)
    {
        var reply = await RequestAsync(
            "catalog.page",
            ClientRequests.GetCatalogPage(pageId),
            m =>
                m.Header == MessageComposer.CatalogPageMessageComposer
                && m.Reader().Int() == pageId,
            ct
        );

        return reply is null ? null : CatalogDecoders.CatalogPage(reply.Reader());
    }

    /// <summary>
    /// Buys one furni the way a player does: opens its page, checks the offer is there, buys
    /// it and waits for it to land in the inventory.
    /// </summary>
    public async Task<InventoryItem?> BuyAsync(FurniOffer offer, CancellationToken ct)
    {
        var page = await OpenCatalogPageAsync(offer.PageId, ct);

        if (page is null)
            return null;

        var listed = page.Offers.FirstOrDefault(x => x.OfferId == offer.OfferId);

        if (
            !_metrics.Check(
                "catalog.page.lists_offer",
                listed is not null
                    && listed.Products.Any(p => p.SpriteId == offer.Definition.SpriteId),
                CheckSeverity.Hard,
                $"{Name}: page {offer.PageId} offer {offer.OfferId} ({offer.Definition.Name})"
            )
        )
            return null;

        var known = _inventory.Keys.ToHashSet();
        var sprite = offer.Definition.SpriteId;

        // The server either adds the furni to the inventory list or, as often, invalidates the
        // list for the client to fetch again; the client takes both.
        var added = Connection.ExpectAsync(
            m =>
                m.Header == MessageComposer.FurniListInvalidateMessageComposer
                || m.Header == MessageComposer.FurniListAddOrUpdateMessageComposer
                    && InventoryDecoders
                        .FurniListAddOrUpdate(m.Reader())
                        .Any(i => i.SpriteId == sprite && !known.Contains(i.ItemId)),
            _options.ReplyTimeout,
            "purchased furni in inventory"
        );

        var result = await RequestAsync(
            "catalog.purchase",
            ClientRequests.PurchaseFromCatalog(offer.PageId, offer.OfferId),
            m =>
                m.Header
                    is MessageComposer.PurchaseOKMessageComposer
                        or MessageComposer.PurchaseErrorMessageComposer
                        or MessageComposer.NotEnoughBalanceMessageComposer
                        or MessageComposer.PurchaseNotAllowedMessageComposer,
            ct
        );

        if (result is null)
            return null;

        if (
            !_metrics.Check(
                "catalog.purchase.succeeds",
                result.Header == MessageComposer.PurchaseOKMessageComposer,
                CheckSeverity.Hard,
                $"{Name}: {offer.Definition.Name} (offer {offer.OfferId}) answered with header {result.Header}"
            )
        )
            return null;

        _metrics.Check(
            "catalog.purchase.confirms_offer",
            CatalogDecoders.PurchaseOk(result.Reader()).OfferId == offer.OfferId,
            CheckSeverity.Hard,
            $"{Name}: offer {offer.OfferId}"
        );

        try
        {
            var update = await added.WaitAsync(ct);

            if (update.Header == MessageComposer.FurniListInvalidateMessageComposer)
            {
                _metrics.Count("inventory.invalidated_after_purchase");
                await LoadInventoryAsync(ct);
            }
        }
        catch (BotTimeoutException)
        {
            // Neither came; look anyway, in case the update slipped past before the wait began.
            await LoadInventoryAsync(ct);
        }

        var item = _inventory.Values.FirstOrDefault(i =>
            i.SpriteId == sprite && !known.Contains(i.ItemId)
        );

        _metrics.Check(
            "catalog.purchase.delivers_to_inventory",
            item is not null,
            CheckSeverity.Hard,
            $"{Name}: {offer.Definition.Name}"
        );

        return item;
    }

    /// <summary>
    /// Fetches the inventory again if the server said it changed (a pickup or a purchase
    /// invalidates it), as the client does before showing it.
    /// </summary>
    public async Task RefreshInventoryIfInvalidAsync(CancellationToken ct)
    {
        if (_inventoryInvalid)
            await LoadInventoryAsync(ct);
    }

    /// <summary>An unplaced floor furni of this sprite the bot already owns, if any.</summary>
    public InventoryItem? OwnedUnplaced(int spriteId) =>
        _inventory.Values.FirstOrDefault(x => x.IsFloor && x.RoomId == 0 && x.SpriteId == spriteId);

    #endregion

    #region Furni

    /// <summary>
    /// Places an inventory furni and waits for the room to show it there. Returns the room
    /// object, or null when the server refused or never answered.
    /// </summary>
    public async Task<FloorItem?> PlaceAsync(
        InventoryItem item,
        int x,
        int y,
        int rotation,
        CancellationToken ct,
        CheckSeverity severity = CheckSeverity.Soft
    )
    {
        var reply = await RequestAsync(
            "furni.place",
            ClientRequests.PlaceFloorItem(item.ItemId, x, y, rotation),
            m =>
                m.Header == MessageComposer.ObjectAddMessageComposer
                && RoomDecoders.ObjectAdd(m.Reader()) is var added
                && added.ObjectId == item.ItemId,
            ct,
            severity: severity
        );

        if (reply is null)
            return null;

        var placed = RoomDecoders.ObjectAdd(reply.Reader());

        _metrics.Check(
            "furni.place.lands_where_asked",
            placed.X == x && placed.Y == y && placed.SpriteId == item.SpriteId,
            CheckSeverity.Hard,
            $"{Name}: item {item.ItemId} asked ({x},{y}) got ({placed.X},{placed.Y}) sprite {placed.SpriteId}/{item.SpriteId}"
        );

        _inventory.TryRemove(item.ItemId, out _);

        return placed;
    }

    public async Task<bool> MoveAsync(
        int objectId,
        int x,
        int y,
        int rotation,
        CancellationToken ct
    )
    {
        var reply = await RequestAsync(
            "furni.move",
            ClientRequests.MoveObject(objectId, x, y, rotation),
            m =>
                m.Header == MessageComposer.ObjectUpdateMessageComposer
                && RoomDecoders.ObjectUpdate(m.Reader()) is var moved
                && moved.ObjectId == objectId
                && moved.X == x
                && moved.Y == y,
            ct,
            severity: CheckSeverity.Soft
        );

        return reply is not null;
    }

    public async Task<bool> PickupAsync(int objectId, CancellationToken ct)
    {
        var reply = await RequestAsync(
            "furni.pickup",
            ClientRequests.PickupFloorItem(objectId),
            m =>
                m.Header == MessageComposer.ObjectRemoveMessageComposer
                && RoomDecoders.ObjectRemove(m.Reader()) == objectId,
            ct
        );

        return reply is not null;
    }

    public Task UseAsync(int objectId, int param, CancellationToken ct) =>
        Connection.SendAsync(ClientRequests.UseFurniture(objectId, param), ct);

    #endregion

    #region Wired

    /// <summary>Opens a wired box's editor and reads what it holds and accepts.</summary>
    /// <remarks>
    /// A probe of a box the server is not expected to answer for is timed and checked apart
    /// (<c>wired.open_known_gap</c>), so its silence stays out of the real editor numbers.
    /// </remarks>
    public async Task<WiredBox?> OpenWiredAsync(
        int objectId,
        CancellationToken ct,
        TimeSpan? timeout = null,
        bool knownGap = false
    )
    {
        var reply = await RequestAsync(
            knownGap ? "wired.open_known_gap" : "wired.open",
            ClientRequests.OpenWired(objectId),
            m => WiredDecoders.KindOf(m.Header) is not null,
            ct,
            timeout,
            knownGap ? CheckSeverity.Soft : CheckSeverity.Hard
        );

        if (reply is null)
            return null;

        var box = WiredDecoders.Box(WiredDecoders.KindOf(reply.Header)!.Value, reply.Reader());

        _metrics.Check(
            "wired.open.is_the_box_asked_for",
            box.ObjectId == objectId,
            CheckSeverity.Hard,
            $"{Name}: opened {objectId}, editor is for {box.ObjectId}"
        );

        return box;
    }

    /// <summary>Saves a wired box: null when the save was answered at all, else the error key.</summary>
    public async Task<(bool Saved, string? Error)> SaveWiredAsync(
        WiredSave save,
        CancellationToken ct
    )
    {
        var reply = await RequestAsync(
            "wired.save",
            ClientRequests.UpdateWired(save),
            m =>
                m.Header
                    is MessageComposer.WiredSaveSuccessMessageComposer
                        or MessageComposer.WiredValidationErrorMessageComposer,
            ct
        );

        if (reply is null)
            return (false, "no reply");

        return reply.Header == MessageComposer.WiredSaveSuccessMessageComposer
            ? (true, null)
            : (false, WiredDecoders.ValidationError(reply.Reader()));
    }

    #endregion
}
