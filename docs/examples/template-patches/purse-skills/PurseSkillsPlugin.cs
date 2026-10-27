using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Turbo.Contracts.Plugins;
using Turbo.Contracts.Plugins.Exports;
using Turbo.Contracts.Plugins.Web;
using Turbo.Messages.Registry;
using Turbo.Primitives.ClientModules;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Networking.Capabilities;
using Turbo.Primitives.Networking.Extensions;
using Turbo.Primitives.Packets;

namespace PurseSkills;

/// <summary>
/// An add-on with both halves: the client module (<c>client/src/index.ts</c>) adds a Skills button to
/// the purse and a window of its own; this server half keeps each player's experience and answers
/// the window over its packets, and serves the hotel's best players over HTTP for the window and
/// for the site (<see cref="TopRoute"/>, <see cref="MeRoute"/>, <c>docs/plugin-web-routes.md</c>). The experience is kept in memory to keep the example small: a real plugin keeps it in
/// its own table (<c>docs/database.md</c>).
/// </summary>
public sealed class PurseSkillsPlugin : ITurboPlugin
{
    /// <summary>Client to server: the window wants the player's skill.</summary>
    public const int GET_SKILL = 20100;

    /// <summary>Server to client: level, experience in the level, experience the level needs.</summary>
    public const int SKILL = 20101;

    /// <summary>Client to server: the window's Train button.</summary>
    public const int TRAIN = 20102;

    private readonly List<IDisposable> _registrations = [];

    public string Key => "skills";

    public string Version => "1.0.0";

    public void ConfigureServices(IServiceCollection services, PluginManifest manifest) =>
        services.AddSingleton<SkillBook>();

    public Task StartAsync(IServiceProvider services, CancellationToken ct)
    {
        var host = services.GetRequiredService<IHostServices>();
        var packets = host.GetRequiredService<IExtensionPacketRegistry>();

        _registrations.Add(
            packets.RegisterParser(GET_SKILL, new Parser(() => new GetSkillMessage()))
        );
        _registrations.Add(packets.RegisterParser(TRAIN, new Parser(() => new TrainMessage())));
        _registrations.Add(packets.RegisterSerializer(typeof(SkillMessage), new SkillSerializer()));
        _registrations.Add(
            host.GetRequiredService<IClientModuleRegistry>().Register(Key, "client/src/index.ts")
        );

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct)
    {
        foreach (var registration in _registrations)
            registration.Dispose();

        _registrations.Clear();

        return Task.CompletedTask;
    }

    public Task BindExportsAsync(IExportBinder binder, IServiceProvider services) =>
        Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private sealed class Parser(Func<IMessageEvent> create) : IParser
    {
        public IMessageEvent Parse(IClientPacket packet) => create();
    }
}

public sealed record GetSkillMessage : IMessageEvent;

public sealed record TrainMessage : IMessageEvent;

/// <summary>Sent only to clients that run client modules: a client without the module would not read it.</summary>
public sealed record SkillMessage(int Level, int Experience, int Needed) : ICapabilityComposer
{
    public string Capability => ClientCapabilities.CLIENT_MODULES;
}

internal sealed class SkillSerializer() : AbstractSerializer<SkillMessage>(PurseSkillsPlugin.SKILL)
{
    protected override void Serialize(IServerPacket packet, SkillMessage message) =>
        packet
            .WriteInteger(message.Level)
            .WriteInteger(message.Experience)
            .WriteInteger(message.Needed);
}

/// <summary>Each player's experience; a level every <see cref="PER_LEVEL"/>.</summary>
public sealed class SkillBook
{
    public const int PER_LEVEL = 100;

    private readonly ConcurrentDictionary<long, int> _experience = new();

    public SkillMessage Of(long playerId) => Message(_experience.GetValueOrDefault(playerId));

    public SkillMessage Train(long playerId, int amount) =>
        Message(_experience.AddOrUpdate(playerId, amount, (_, current) => current + amount));

    /// <summary>The players with the most experience, most first.</summary>
    public IReadOnlyList<(long PlayerId, int Experience)> Top(int count) =>
        [
            .. _experience
                .OrderByDescending(x => x.Value)
                .ThenBy(x => x.Key)
                .Take(count)
                .Select(x => (x.Key, x.Value)),
        ];

    private static SkillMessage Message(int total) =>
        new(1 + (total / PER_LEVEL), total % PER_LEVEL, PER_LEVEL);
}

public sealed class GetSkillHandler(SkillBook book) : IMessageHandler<GetSkillMessage>
{
    public async ValueTask HandleAsync(
        GetSkillMessage message,
        MessageContext ctx,
        CancellationToken ct
    ) => await ctx.SendComposerAsync(book.Of(ctx.PlayerId.Value), ct).ConfigureAwait(false);
}

public sealed class TrainHandler(SkillBook book) : IMessageHandler<TrainMessage>
{
    public async ValueTask HandleAsync(
        TrainMessage message,
        MessageContext ctx,
        CancellationToken ct
    ) => await ctx.SendComposerAsync(book.Train(ctx.PlayerId.Value, 25), ct).ConfigureAwait(false);
}

/// <summary>
/// <c>GET /api/plugins/skills/top</c>: the ten players with the most experience. Anyone may ask,
/// signed in or not, so the hotel's site can show it on a page.
/// </summary>
public sealed class TopRoute(SkillBook book) : IPluginWebRoute
{
    public string Method => "GET";

    public string Pattern => "top";

    public bool Anonymous => true;

    public Task<PluginWebResult> HandleAsync(PluginWebRequest request, CancellationToken ct) =>
        Task.FromResult(
            PluginWebResult.Ok(
                book.Top(10)
                    .Select(x => new
                    {
                        playerId = x.PlayerId,
                        level = 1 + (x.Experience / SkillBook.PER_LEVEL),
                        experience = x.Experience,
                    })
                    .ToList()
            )
        );
}

/// <summary>
/// <c>GET /api/plugins/skills/me</c>: the signed-in player's own skill. The host answers 401 to
/// someone who is not signed in, so <see cref="PluginWebRequest.PlayerId"/> is always set here.
/// </summary>
public sealed class MeRoute(SkillBook book) : IPluginWebRoute
{
    public string Method => "GET";

    public string Pattern => "me";

    public Task<PluginWebResult> HandleAsync(PluginWebRequest request, CancellationToken ct)
    {
        var skill = book.Of(request.PlayerId!.Value);

        return Task.FromResult(
            PluginWebResult.Ok(
                new
                {
                    level = skill.Level,
                    experience = skill.Experience,
                    needed = skill.Needed,
                }
            )
        );
    }
}
