using System;

namespace Turbo.Primitives.Players.Permissions;

/// <summary>
/// Lets a packet handler run for a session that has no signed-in player yet. Every other handler
/// is reached only once the session has logged in: the pipeline (<c>SignInGate</c>) drops a packet
/// from a session without a player before its handler runs, without a reply. Only what the client
/// sends before or while it logs in carries this: the handshake, the SSO ticket and pings.
/// <c>AllowAnonymousHandlerTests</c> lists every handler that carries it, so adding one is a
/// deliberate change. See <c>docs/permissions.md</c> §11.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class AllowAnonymousAttribute : Attribute;
