using Orleans;
using Turbo.Primitives.Networking.Capabilities;

namespace Turbo.Primitives.Messages.Outgoing.Turbo;

/// <summary>
/// A Turbo extension, not Habbo's (<c>web.session</c>): the token a client sends plugins' web
/// routes as <c>Authorization: Bearer</c>, the address they are under (before
/// <c>/api/plugins/</c>) and how many seconds it lasts. An empty token: the hotel serves none.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record TurboWebSessionMessage : ICapabilityComposer
{
    [Id(0)]
    public required string BaseUrl { get; init; }

    [Id(1)]
    public required string Token { get; init; }

    [Id(2)]
    public required int ExpiresInSeconds { get; init; }

    public string Capability => ClientCapabilities.WEB_SESSION;
}
