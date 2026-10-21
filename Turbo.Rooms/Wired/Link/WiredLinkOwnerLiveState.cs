using System.Collections.Generic;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Wired.Link;

namespace Turbo.Rooms.Wired.Link;

/// <summary>What a <see cref="WiredLinkOwnerGrain"/> holds: the owner's networks and Link configurations.</summary>
internal sealed class WiredLinkOwnerLiveState
{
    public PlayerId PlayerId { get; set; }

    /// <summary>Network names by id.</summary>
    public Dictionary<int, string> Networks { get; } = [];

    public Dictionary<WiredLinkEndpoint, LinkState> Links { get; } = [];

    public sealed class LinkState
    {
        public int? NetworkId { get; set; }

        public bool ReceiveLocal { get; set; }

        public bool IsBuildersClub { get; set; }
    }
}
