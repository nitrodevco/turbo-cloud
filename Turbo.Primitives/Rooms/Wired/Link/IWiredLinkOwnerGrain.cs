using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Orleans;

namespace Turbo.Primitives.Rooms.Wired.Link;

/// <summary>
/// One player's WIRED Links networks and the configuration of every Link they own, in any room,
/// loaded or not. Keyed by the player id. Everything a Link's window does goes through here, and
/// so does every send: it knows which Links a network reaches.
/// </summary>
public interface IWiredLinkOwnerGrain : IGrainWithIntegerKey
{
    public Task<WiredLinkConfigurationSnapshot> GetConfigurationAsync(
        WiredLinkEndpoint link,
        CancellationToken ct
    );

    public Task<WiredLinkOperationOutcome> SaveAsync(
        WiredLinkEndpoint link,
        bool isBuildersClub,
        string networkId,
        bool receiveLocal,
        CancellationToken ct
    );

    public Task<WiredLinkOperationOutcome> CreateNetworkAsync(
        WiredLinkEndpoint link,
        string name,
        CancellationToken ct
    );

    public Task<WiredLinkOperationOutcome> RenameNetworkAsync(
        WiredLinkEndpoint link,
        string networkId,
        string name,
        CancellationToken ct
    );

    public Task<WiredLinkOperationOutcome> DeleteNetworkAsync(
        WiredLinkEndpoint link,
        string networkId,
        CancellationToken ct
    );

    /// <summary>The Links connected to a network, or null when it is not one of the owner's.</summary>
    public Task<ImmutableArray<WiredLinkConnectionSnapshot>?> GetConnectionsAsync(
        string networkId,
        CancellationToken ct
    );

    /// <summary>The Link was picked up: it keeps no connection.</summary>
    public Task DisconnectAsync(WiredLinkEndpoint link, CancellationToken ct);

    /// <summary>
    /// Sends <paramref name="payload"/> through the owner's Links <paramref name="linkIds"/> in
    /// <paramref name="roomId"/>; answers how many Links it was delivered to.
    /// </summary>
    public Task<int> SendAsync(
        RoomId roomId,
        ImmutableArray<int> linkIds,
        WiredLinkPayload payload,
        CancellationToken ct
    );
}
