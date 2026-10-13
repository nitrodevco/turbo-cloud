using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Preferences;

/// <summary>
/// A change to the player's own word filter (<c>ModifyCustomFilterResultMessageEventParser</c>):
/// the client adds the word on result 1 and takes it out on 3, and leaves its list as it is on
/// anything else (<c>WordFilterSettingsView.onModifyCustomFilter</c>).
/// </summary>
[GenerateSerializer, Immutable]
public sealed record ModifyCustomFilterResultMessageComposer : IComposer
{
    public const int RESULT_ADDED = 1;
    public const int RESULT_REMOVED = 3;

    [Id(0)]
    public required int Result { get; init; }

    [Id(1)]
    public required string Word { get; init; }
}
