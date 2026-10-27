using System;
using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Pipeline.Registry;

/// <summary>
/// Middleware around an envelope's handlers (a packet's, an event's), run in <c>[Order]</c>. It
/// passes the envelope on by awaiting <c>next</c>, or stops it by returning without.
/// <para>
/// Fail closed: a behavior that throws before it calls <c>next</c>, or that cannot be built, drops
/// the envelope. The behaviors after it and the handlers never see it, the failure is logged, and
/// the host answers nobody, since it does not know what the envelope asked for. A behavior that
/// stops a packet the client waits on (a purchase, a trade step) sends the client its reply itself
/// before returning, or the client is left waiting; a rule about a game action belongs in that
/// action's policy (<c>IPurchasePolicy</c> and the others), which answers the client for it.
/// </para>
/// </summary>
public interface IBehavior<in TEnvelope, in TContext>
{
    ValueTask InvokeAsync(TEnvelope env, TContext ctx, Func<ValueTask> next, CancellationToken ct);
}
