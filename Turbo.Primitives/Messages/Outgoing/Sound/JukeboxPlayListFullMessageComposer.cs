using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Sound;

/// <summary>A disk was refused because the jukebox's playlist is full; the client alerts.</summary>
[GenerateSerializer, Immutable]
public sealed record JukeboxPlayListFullMessageComposer : IComposer;
