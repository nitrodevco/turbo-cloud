using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;

/// <summary>
/// The key <c>WiredGenerateWebApiKey</c> asked for (Flash <c>WiredWebApiKeyResultParser</c>:
/// int wiredId, bool isReadKey, string key). The editor puts it in the matching field
/// (<c>VariablesWebApiAddon.onWiredApiKeyResultEvent</c>); it is kept once the box is saved.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record WiredWebApiKeyResultMessageComposer : IComposer
{
    [Id(0)]
    public required int WiredId { get; init; }

    [Id(1)]
    public required bool IsReadKey { get; init; }

    [Id(2)]
    public required string Key { get; init; }
}
