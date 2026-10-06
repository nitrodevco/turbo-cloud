using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Admin.Configuration;
using Turbo.Events.Registry;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Events;

namespace Turbo.Admin.Links;

/// <summary>
/// The hotel's owner has no passkey yet: print their setup link in the server log, so a hotel
/// whose server has no terminal to attach to (a Ploi daemon) can still get its first admin in
/// from the log alone. The link is the one <c>adminsetup</c> makes: it works once and for a few
/// hours, and replaces nothing, because there is nothing to replace. An owner who has a passkey
/// already is left alone.
/// </summary>
public sealed class OwnerSetupLinkHandler(
    AdminLinkPolicy policy,
    IGrainFactory grainFactory,
    IOptions<AdminConfig> config,
    ILogger<OwnerSetupLinkHandler> logger
) : IEventHandler<OwnerConfirmedEvent>
{
    public async ValueTask HandleAsync(
        OwnerConfirmedEvent env,
        EventContext ctx,
        CancellationToken ct
    )
    {
        var options = config.Value;

        if (!options.Enabled)
            return;

        var decision = await policy.CheckAsync(null, env.PlayerId, ct).ConfigureAwait(false);

        if (decision.ReplacesExisting)
            return;

        var link = await grainFactory
            .GetAdminAuthGrain()
            .CreateSetupTokenAsync(env.PlayerId, ct)
            .ConfigureAwait(false);

        logger.LogWarning(
            "Admin panel setup for the owner, {Name}: open {Url} on the device you will sign in with. "
                + "It works once, until {Expires}. Anyone who has it can sign in as {Name}.",
            env.Name,
            $"{options.PanelUrl.TrimEnd('/')}/setup#token={link.Token}",
            link.ExpiresAtUtc.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture),
            env.Name
        );
    }
}
