using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Turbo;

/// <summary>
/// A Turbo extension, not Habbo's (<c>web.session</c>): a signed-in client asking for a token to
/// call plugins' web routes as its player. Answered with <c>TurboWebSessionMessage</c>.
/// </summary>
public record TurboWebSessionRequestMessage : IMessageEvent;
