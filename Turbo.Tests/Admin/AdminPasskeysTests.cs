using System.Buffers.Binary;
using System.Reflection;
using System.Text.Json;
using Fido2NetLib;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Turbo.Admin.Configuration;
using Turbo.Primitives.Admin.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Admin;

/// <summary>
/// The admin panel's passkey checks, driven by a software authenticator through the same JSON a
/// browser sends: a passkey made at setup signs its owner in, and an answer for another site, from
/// an authenticator that did not verify the person, for another player, or replayed, does not.
/// </summary>
public sealed class AdminPasskeysTests : IDisposable
{
    private const string PANEL = "https://admin.example.com";
    private static readonly PlayerId ALICE = new(7);
    private static readonly PlayerId BOB = new(8);

    private readonly SoftwareAuthenticator _authenticator = new(PANEL);
    private readonly object _passkeys;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public AdminPasskeysTests() =>
        _passkeys = Activator.CreateInstance(
            typeof(AdminConfig).Assembly.GetType("Turbo.Admin.Api.AdminPasskeys")!,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            [Options.Create(new AdminConfig { PanelUrl = PANEL })],
            culture: null
        )!;

    public void Dispose() => _authenticator.Dispose();

    [Fact]
    public async Task APasskeyMadeAtSetupSignsItsOwnerIn()
    {
        var stored = await RegisterAsync(ALICE);
        var options = SignInOptions(stored);

        var counter = await SignInAsync(
            stored,
            ALICE,
            _authenticator.SignIn(options, Handle(ALICE)),
            options
        );

        counter.Should().Be(1u);
    }

    [Fact]
    public void TheOptionsBindThePasskeyToThePanelsDomainAndAskForTheUser()
    {
        var options = JsonDocument.Parse(RegistrationOptions(ALICE)).RootElement;

        options.GetProperty("rp").GetProperty("id").GetString().Should().Be("admin.example.com");
        options
            .GetProperty("authenticatorSelection")
            .GetProperty("userVerification")
            .GetString()
            .Should()
            .Be("required");
        // Discoverable, so signing in needs no name: the passkey says whose it is.
        options
            .GetProperty("authenticatorSelection")
            .GetProperty("residentKey")
            .GetString()
            .Should()
            .Be("required");
    }

    [Fact]
    public void APasskeysUserHandleNamesItsPlayer()
    {
        var playerOf = _passkeys.GetType().GetMethod("PlayerOf")!;

        playerOf.Invoke(null, [Handle(ALICE)]).Should().Be(ALICE);
        playerOf.Invoke(null, [new byte[] { 1, 2, 3 }]).Should().BeNull();
        playerOf.Invoke(null, [new byte[] { 0, 0, 0, 0 }]).Should().BeNull();
        playerOf.Invoke(null, [null]).Should().BeNull();
    }

    [Fact]
    public async Task AnAnswerMadeForAnotherSiteIsRefused()
    {
        var stored = await RegisterAsync(ALICE);
        var options = SignInOptions(stored);
        var answer = _authenticator.SignIn(
            options,
            Handle(ALICE),
            asOrigin: "https://admin.example.com.evil.test"
        );

        var signIn = () => SignInAsync(stored, ALICE, answer, options);

        await signIn.Should().ThrowAsync<Fido2VerificationException>().WithMessage("*origin*");
    }

    [Fact]
    public async Task AnAuthenticatorThatDidNotVerifyThePersonIsRefused()
    {
        var stored = await RegisterAsync(ALICE);
        var options = SignInOptions(stored);
        _authenticator.VerifiesUser = false;
        var answer = _authenticator.SignIn(options, Handle(ALICE));

        var signIn = () => SignInAsync(stored, ALICE, answer, options);

        await signIn.Should().ThrowAsync<Fido2VerificationException>().WithMessage("*erif*");
    }

    [Fact]
    public async Task APasskeyOfAnotherPlayerIsRefused()
    {
        var alices = await RegisterAsync(ALICE);
        var options = SignInOptions(alices);
        var answer = _authenticator.SignIn(options, Handle(ALICE));

        var signIn = () => SignInAsync(alices, BOB, answer, options);

        await signIn.Should().ThrowAsync<Fido2VerificationException>().WithMessage("*not owner*");
    }

    [Fact]
    public async Task AReplayedSignInIsRefused()
    {
        var stored = await RegisterAsync(ALICE);
        var options = SignInOptions(stored);
        var answer = _authenticator.SignIn(options, Handle(ALICE));
        var counter = await SignInAsync(stored, ALICE, answer, options);

        var replay = () => SignInAsync(stored with { SignCount = counter }, ALICE, answer, options);

        await replay.Should().ThrowAsync<Fido2VerificationException>().WithMessage("*ount*");
    }

    private async Task<AdminPasskeyCredential> RegisterAsync(PlayerId playerId)
    {
        var options = RegistrationOptions(playerId);
        var answer = _authenticator.Register(options);

        return await Invoke<Task<AdminPasskeyCredential>>(
            "CompleteRegistrationAsync",
            options,
            answer.Deserialize<AuthenticatorAttestationRawResponse>()!,
            Ct
        );
    }

    private string RegistrationOptions(PlayerId playerId) =>
        Invoke<CredentialCreateOptions>(
                "BeginRegistration",
                playerId,
                "alice",
                Array.Empty<byte[]>()
            )
            .ToJson();

    private string SignInOptions(AdminPasskeyCredential stored) =>
        Invoke<Fido2NetLib.AssertionOptions>("BeginSignIn", (object)new[] { stored.CredentialId })
            .ToJson();

    private Task<uint> SignInAsync(
        AdminPasskeyCredential stored,
        PlayerId playerId,
        JsonElement answer,
        string? options = null
    ) =>
        Invoke<Task<uint>>(
            "CompleteSignInAsync",
            options ?? SignInOptions(stored),
            answer.Deserialize<AuthenticatorAssertionRawResponse>()!,
            stored,
            playerId,
            Ct
        );

    private T Invoke<T>(string method, params object[] args)
    {
        try
        {
            return (T)_passkeys.GetType().GetMethod(method)!.Invoke(_passkeys, args)!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw ex.InnerException;
        }
    }

    private static byte[] Handle(PlayerId playerId)
    {
        var handle = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(handle, playerId.Value);

        return handle;
    }
}
