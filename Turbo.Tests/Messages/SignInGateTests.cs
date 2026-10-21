using System.Collections.Concurrent;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SuperSocket.Server.Abstractions.Session;
using Turbo.Messages;
using Turbo.Messages.Extensions;
using Turbo.Messages.Registry;
using Turbo.Networking.Extensions;
using Turbo.Networking.Package;
using Turbo.Networking.Revisions;
using Turbo.PacketHandlers.Marketplace;
using Turbo.Primitives.Catalog.Marketplace;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Players;
using Turbo.Revisions.Revision20260909;
using Turbo.Runtime.AssemblyProcessing;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Messages;

/// <summary>
/// What <see cref="SignInGate"/> lets through, driven the way the server runs a packet: client
/// bytes into <see cref="PackageHandler"/>, the revision's parser, and the message system with
/// every handler in <c>Turbo.PacketHandlers</c> registered by the message feature processor the
/// host registers (<c>AddTurboMessageSystem</c>). Services a handler needs are recording fakes.
/// </summary>
public sealed class SignInGateTests : IAsyncLifetime
{
    private const int OFFER_ID = 42;

    private readonly Fakes _fakes = new();
    private readonly ConcurrentQueue<IComposer> _sent = new();
    private readonly CapturingLogger<SignInGateTests> _log = new();
    private readonly RevisionManager _revisions = new(NullLogger<RevisionManager>.Instance);
    private readonly ISessionContext _session;
    private ServiceProvider _container = null!;
    private IDisposable _registrations = null!;
    private PackageHandler _packages = null!;

    // The player the gateway has for the session: none until a test signs it in.
    private PlayerId _signedIn;

    public SignInGateTests()
    {
        _revisions.RegisterRevision(new Revision20260909());
        _fakes.Handlers["get_RevisionId"] = _ => _revisions.DefaultRevisionId;
        _fakes.Handlers["get_SessionKey"] = _ => (SessionKey)"session-1";
        _fakes.Handlers["GetPlayerId"] = _ => _signedIn;
        _fakes.Handlers["SendComposerAsync"] = call =>
        {
            foreach (var arg in call.Args)
                if (arg is IComposer composer)
                    _sent.Enqueue(composer);

            return Fakes.NotHandled;
        };
        _fakes.Handlers["BuyOfferAsync"] = _ =>
            Task.FromResult(
                new MarketplaceBuyResult(MarketplaceBuyResultCode.Bought, OFFER_ID, 0, OFFER_ID)
            );
        _fakes.Handlers["GetSignedPrime"] = _ => "prime";
        _fakes.Handlers["GetSignedGenerator"] = _ => "generator";
        _session = _fakes.Create<ISessionContext>("session");
    }

    public async ValueTask InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(new CapturingLoggerFactory(_log));
        services.AddLogging();
        services.AddTurboMessageSystem();
        services.AddSingleton(_fakes.Create<ISessionGateway>());
        _container = services.BuildServiceProvider();

        _registrations = await _container
            .GetServices<IAssemblyFeatureProcessor>()
            .Single()
            .ProcessAsync(
                typeof(BuyMarketplaceOfferMessageHandler).Assembly,
                new FakingServiceProvider(_container, _fakes)
            );

        _packages = new PackageHandler(
            _revisions,
            new ExtensionPacketRegistry(),
            _container.GetRequiredService<MessageSystem>(),
            _container.GetRequiredService<ILogger<PackageHandler>>()
        );
    }

    public async ValueTask DisposeAsync()
    {
        _registrations.Dispose();
        await _container.DisposeAsync();
    }

    [Fact]
    public async Task Marketplace_FromASessionThatHasNotLoggedIn_ReachesNothing()
    {
        var replies = await BuyOfferAsync();

        _fakes.Log.Of("BuyOfferAsync").Should().BeEmpty();
        replies.Should().BeEmpty();
        _log.Entries.Should()
            .ContainSingle(x =>
                x.Level == LogLevel.Debug
                && x.Message.StartsWith(
                    "Dropped BuyMarketplaceOfferMessage ",
                    StringComparison.Ordinal
                )
            );
        _fakes.Log.Of("CloseSessionAsync").Should().BeEmpty();
    }

    [Fact]
    public async Task Marketplace_FromASignedInPlayer_IsBoughtAndAnswered()
    {
        _signedIn = 7;

        var replies = await BuyOfferAsync();

        _fakes
            .Log.Of("BuyOfferAsync")
            .Should()
            .ContainSingle()
            .Which.Args.Take(2)
            .Should()
            .Equal((PlayerId)7, OFFER_ID);
        replies
            .Select(x => x.Header)
            .Should()
            .Equal(PacketHarness.Outgoing("MarketplaceBuyOfferResultMessageComposer"));
        _log.AtLeast(LogLevel.Warning).Should().BeEmpty();
    }

    [Fact]
    public async Task Handshake_FromASessionThatHasNotLoggedIn_IsAnswered()
    {
        var replies = await SendAsync("InitDiffieHandshakeMessageEvent", []);

        replies
            .Select(x => x.Header)
            .Should()
            .Equal(PacketHarness.Outgoing("InitDiffieHandshakeMessageComposer"));
        _log.AtLeast(LogLevel.Warning).Should().BeEmpty();
    }

    [Fact]
    public async Task SsoTicket_FromASessionThatHasNotLoggedIn_IsChecked()
    {
        await SendAsync(
            "SSOTicketMessageEvent",
            PacketHarness.Payload(w => w.String("ticket").Int(0))
        );

        // The fake authentication service knows no ticket, so the session is turned away: the
        // handler ran.
        _fakes.Log.Of("GetPlayerIdFromTicketAsync").Should().ContainSingle();
        _fakes.Log.Of("CloseSessionAsync").Should().ContainSingle();
    }

    private Task<List<ClientPacket>> BuyOfferAsync() =>
        SendAsync("BuyMarketplaceOfferMessageEvent", PacketHarness.Payload(w => w.Int(OFFER_ID)));

    private async Task<List<ClientPacket>> SendAsync(string incoming, byte[] payload)
    {
        await _packages.Handle(
            (IAppSession)_session,
            new ClientPacket(PacketHarness.Incoming(incoming), payload),
            CancellationToken.None
        );

        return [.. _sent.Select(PacketHarness.Encode)];
    }

    /// <summary>The container, then a recording fake for any other interface.</summary>
    private sealed class FakingServiceProvider(IServiceProvider inner, Fakes fakes)
        : IServiceProvider
    {
        public object? GetService(Type serviceType) =>
            inner.GetService(serviceType)
            ?? (
                serviceType is { IsInterface: true, IsGenericType: false }
                    ? fakes.Create(serviceType)
                    : null
            );
    }

    private sealed class CapturingLoggerFactory(ILogger logger) : ILoggerFactory
    {
        public ILogger CreateLogger(string categoryName) => logger;

        public void AddProvider(ILoggerProvider provider) { }

        public void Dispose() { }
    }
}
