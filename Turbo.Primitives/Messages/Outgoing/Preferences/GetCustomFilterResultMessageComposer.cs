using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Preferences;

/// <summary>The words in the player's own word filter (<c>GetCustomFilterResultMessageEventParser</c>).</summary>
[GenerateSerializer, Immutable]
public sealed record GetCustomFilterResultMessageComposer : IComposer
{
    [Id(0)]
    public required List<string> Words { get; init; }
}
