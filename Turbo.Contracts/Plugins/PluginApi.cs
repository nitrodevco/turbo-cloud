namespace Turbo.Contracts.Plugins;

/// <summary>
/// The version of what plugins build against (these contracts and the host services they are
/// handed). It goes up when plugins get something new to build on; a plugin declares the one it
/// was built for in its manifest's <see cref="PluginManifest.ApiVersion"/>, and a server takes
/// plugins built for <see cref="OLDEST"/> up to its own <see cref="VERSION"/>.
/// </summary>
public static class PluginApi
{
    /// <summary>
    /// 1: everything plugins build on today, plugin settings, admin endpoints and pages
    /// (<c>Admin.IPluginAdmin</c>), asset movement consumers and reversals
    /// (<c>docs/movements.md</c>) and plugin notices (<c>docs/plugin-notices.md</c>) included. An
    /// addition raises <see cref="VERSION"/>; a change that breaks a published plugin raises
    /// <see cref="OLDEST"/> with it.
    /// <para>
    /// 2: the convention base <see cref="TurboPlugin"/> (key and version from the manifest, its
    /// <c>wwwroot/&lt;key&gt;.js</c> registered by the host, no class needed at all),
    /// <see cref="PluginSettingsAttribute"/>, and plugin database contexts with a design-time
    /// factory of their own. A plugin built for 1 loads as before.
    /// </para>
    /// <para>
    /// 3: the wired kit: wired boxes (<c>Wired.WiredEffect</c>, <c>WiredTrigger&lt;TEvent&gt;</c>,
    /// <c>WiredCondition</c>, <c>WiredSelector</c>) and furni logic (<c>Furni.FurniLogic</c>) built
    /// against these contracts alone, and plugin wired events (<c>Hotel.IHotel.Wired</c>,
    /// <c>docs/plugin-wired.md</c>). A plugin built for 1 or 2 loads as before.
    /// </para>
    /// <para>
    /// 4: the plugin's lifetime (<see cref="TurboPlugin"/>'s <c>HotelReadyAsync</c>,
    /// <see cref="IPluginContext.Stopping"/>, its registered services built over the host's),
    /// per-server scheduled tasks (<c>PerServer</c>), furni grants under a reference
    /// (<c>Hotel.GrantOutcome</c>) and the fake hotel's <c>FailNext</c> and starting ids. A plugin
    /// built for 1 to 3 loads as before.
    /// </para>
    /// <para>
    /// 5: who plays and from where: the client's address on the login's play policies and on the
    /// login and registration events, the login and registration failure events, registration
    /// policies (<c>Policies.IRegistrationPolicy</c>), room ads through the purchase policies and
    /// <c>PurchaseCompletedEvent.RoomAd</c>, and <c>Hotel.IHotel.Players</c>' recent players and
    /// identity links. A plugin built for 1 to 4 loads as before.
    /// </para>
    /// <para>
    /// 6: data plugins - a manifest with no <see cref="PluginManifest.AssemblyFile"/> loads its files
    /// only - and theme style properties (<c>theme.json</c>, <c>docs/plugin-data.md</c>). A server
    /// before 6 refuses a manifest with no assembly; a plugin built for 1 to 5 loads as before.
    /// </para>
    /// <para>
    /// 7: the hotel's open or closed state (<c>Hotel.IHotel.Status</c>, and
    /// <c>HotelAvailabilityChangedEvent</c> on each change), and string-keyed dictionaries in named
    /// plugin messages. A plugin built for 1 to 6 loads as before.
    /// </para>
    /// <para>
    /// 8: replaceable host services (<c>Primitives.Plugins.ReplaceableServiceAttribute</c>,
    /// <c>IServiceSlot&lt;T&gt;</c>, <c>docs/plugin-services.md</c>): a plugin that registers its
    /// own campaign calendar, trade policy or game access policy replaces the host's for the whole
    /// host while it is live. A plugin built for 1 to 7 loads as before.
    /// </para>
    /// <para>
    /// 9: avatar effect policies (<c>Policies.IAvatarEffectPolicy</c>: what an avatar wears and what
    /// each player is shown), purchase fulfilments (<c>Policies.IPurchaseFulfilment</c>: an offer or
    /// room ad handed over by the plugin in the hotel's place), a player's time online and respects
    /// (<c>Hotel.IHotelPlayers.OnlineTimeAsync</c>, <c>RespectsAsync</c>), and online reward rules of
    /// their own for rooms only, idling and sleeping. A plugin built for 1 to 8 loads as before.
    /// </para>
    /// </summary>
    public const int VERSION = 9;

    /// <summary>The oldest plugin API this server still loads.</summary>
    public const int OLDEST = 1;

    /// <summary>Why the server can't take a plugin built for <paramref name="manifest"/>'s API; null when it can.</summary>
    public static string? Refusal(PluginManifest manifest) =>
        manifest.ApiVersion is { } api && (api < OLDEST || api > VERSION)
            ? $"{manifest.Name} {manifest.Version} was built for plugin API {api}{(manifest.BuiltAgainst is { } built ? $" (turbo-cloud {built})" : "")}; this server has plugin API {VERSION} (it loads {OLDEST} to {VERSION}). Install a build made for it."
            : null;
}
