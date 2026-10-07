using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orleans;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Messages.Outgoing.Turbo;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Snapshots.Permissions;
using Turbo.Primitives.Texts;

namespace Turbo.Commands;

/// <summary>
/// Sends players their command tree: when a client accepts <c>chat.commands</c>, when what a
/// player may use changes (<see cref="CommandTreePermissionsHandler"/>), and to everyone online
/// when the commands themselves change, a plugin loading or unloading or the texts reloading.
/// The presence drops it for a session that did not accept the extension, so nothing here asks.
/// </summary>
public sealed class CommandTreeService : ICommandTreeService, IDisposable
{
    private readonly ICommandRegistryProvider _registryProvider;
    private readonly IHotelTextProvider _textProvider;
    private readonly IGrainFactory _grainFactory;
    private readonly ISessionGateway _sessionGateway;
    private readonly ILogger<ICommandTreeService> _logger;

    public CommandTreeService(
        ICommandRegistryProvider registryProvider,
        IHotelTextProvider textProvider,
        IGrainFactory grainFactory,
        ISessionGateway sessionGateway,
        ILogger<ICommandTreeService> logger
    )
    {
        _registryProvider = registryProvider;
        _textProvider = textProvider;
        _grainFactory = grainFactory;
        _sessionGateway = sessionGateway;
        _logger = logger;

        _registryProvider.Changed += OnRegistryChanged;
    }

    public async Task SendAsync(PlayerId playerId, CancellationToken ct) =>
        await SendAsync(
            playerId,
            await _grainFactory.GetPlayerPermissionGrain(playerId).GetResolvedAsync(ct),
            ct
        );

    /// <summary>Sends the tree for permissions the caller already holds, sparing the grain a call.</summary>
    public async Task SendAsync(
        PlayerId playerId,
        ResolvedPermissionsSnapshot permissions,
        CancellationToken ct
    )
    {
        var registry = _registryProvider.Current;
        var texts = await _textProvider
            .GetTextsAsync(CommandHelp.DescriptionKeys(registry.Commands), ct)
            .ConfigureAwait(false);

        await _grainFactory
            .SendComposerToPlayerAsync(
                playerId,
                new TurboCommandTreeMessage
                {
                    Tree = CommandTreeBuilder.Build(registry, permissions, texts),
                },
                ct
            )
            .ConfigureAwait(false);
    }

    public void Dispose() => _registryProvider.Changed -= OnRegistryChanged;

    /// <summary>
    /// Raised inside a registration, so the sends are not awaited there: each is its own grain
    /// call, logged if it fails, and a plugin load never waits on the players online.
    /// </summary>
    private void OnRegistryChanged()
    {
        foreach (var playerId in _sessionGateway.GetOnlinePlayerIds())
            SendAsync(playerId, CancellationToken.None)
                .LogAndForget(_logger, "send the command tree to player {PlayerId}", playerId);
    }
}
