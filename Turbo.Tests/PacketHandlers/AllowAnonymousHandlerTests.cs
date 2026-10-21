using System.Reflection;
using FluentAssertions;
using Turbo.PacketHandlers.Handshake;
using Turbo.PacketHandlers.Tracking;
using Turbo.Primitives.Players.Permissions;
using Xunit;

namespace Turbo.Tests.PacketHandlers;

/// <summary>
/// Pins the packet handlers a session reaches before it has logged in. Every other handler is
/// dropped by <c>SignInGate</c> until the session has a player, so adding one here is a decision
/// about what an unauthenticated connection may do, not a fix for a failing test: it belongs only
/// to what the client sends before <c>AuthenticationOK</c> (Flash's <c>IncomingMessages</c> and
/// <c>HabboCommunicationDemo.sendConnectionParameters</c>, nitro-next's
/// <c>WebSocketContextProvider</c>).
/// </summary>
public class AllowAnonymousHandlerTests
{
    private static readonly Type[] ANONYMOUS =
    [
        // The connection's first packet: names the client's production, so the revision is set.
        typeof(ClientHelloMessageHandler),
        // Flash starts the key exchange as the socket opens, long before the ticket.
        typeof(InitDiffieHandshakeMessageHandler),
        typeof(CompleteDiffieHandshakeMessageHandler),
        // Sent with the ticket by sendConnectionParameters.
        typeof(VersionCheckMessageHandler),
        typeof(UniqueIdMessageHandler),
        // The login itself.
        typeof(SSOTicketMessageHandler),
        // The answer to the server's Ping, which can come before the login completes.
        typeof(PongMessageHandler),
        // Sent as the client unloads, whether or not it ever logged in.
        typeof(DisconnectMessageHandler),
        // Flash's LatencyTracker starts on configuration, not on login; the reply is an echo.
        typeof(LatencyPingRequestMessageHandler),
    ];

    [Fact]
    public void OnlyThePreLoginHandlers_AllowAnonymousSessions()
    {
        PermissionGateTests
            .HandlerTypes()
            .Where(x => x.GetCustomAttribute<AllowAnonymousAttribute>() is not null)
            .Should()
            .BeEquivalentTo(ANONYMOUS);
    }

    [Fact]
    public void NoAnonymousHandler_DeclaresAPermission()
    {
        // A permission needs a player, so the two attributes together would be a handler nobody
        // could reach before login anyway.
        ANONYMOUS
            .Where(x => x.GetCustomAttribute<RequiresPermissionAttribute>() is not null)
            .Should()
            .BeEmpty();
    }
}
