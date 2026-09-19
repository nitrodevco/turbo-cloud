using System;
using System.Collections.Generic;
using Turbo.Primitives.Players;

namespace Turbo.Players.Grains;

/// <summary>The directory is one grain for the hotel, so its state carries no key.</summary>
internal sealed class PlayerDirectoryLiveState
{
    public Dictionary<PlayerId, string> IdToName { get; } = [];
    public Dictionary<string, PlayerId> NameToId { get; } = new(StringComparer.OrdinalIgnoreCase);
}
