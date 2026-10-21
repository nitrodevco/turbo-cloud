using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Org.BouncyCastle.Crypto.Encodings;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Utilities.Encoders;
using Turbo.Crypto;
using Turbo.Crypto.Configuration;
using Turbo.Primitives.Crypto;
using Turbo.Primitives.Packets;
using Turbo.Tests.Support;
using Xunit;
using BigInteger = Org.BouncyCastle.Math.BigInteger;

namespace Turbo.Tests.PacketHandlers;

/// <summary>
/// The Flash client's key exchange, as it sends it: one Init and one Complete on each connection.
/// What the server signs for it is the same for every session and is signed once; a session that
/// has exchanged keys cannot exchange them again, which would replace its ciphers mid-stream.
/// </summary>
public class DiffieHandshakeTests
{
    private static readonly int INIT = PacketHarness.Incoming("InitDiffieHandshakeMessageEvent");
    private static readonly int COMPLETE = PacketHarness.Incoming(
        "CompleteDiffieHandshakeMessageEvent"
    );
    private static readonly int INIT_REPLY = PacketHarness.Outgoing(
        "InitDiffieHandshakeMessageComposer"
    );
    private static readonly int COMPLETE_REPLY = PacketHarness.Outgoing(
        "CompleteDiffieHandshakeMessageComposer"
    );

    // A signed-out session, as the handshake runs before the SSO ticket.
    private const int NO_PLAYER = -1;

    private static readonly RsaKeyParameters PublicKey;
    private static readonly CryptoConfig Config;

    static DiffieHandshakeTests()
    {
        var generator = new RsaKeyPairGenerator();
        generator.Init(
            new RsaKeyGenerationParameters(
                BigInteger.ValueOf(0x10001),
                new SecureRandom(),
                1024,
                25
            )
        );
        var pair = generator.GenerateKeyPair();
        var priv = (RsaKeyParameters)pair.Private;

        PublicKey = (RsaKeyParameters)pair.Public;
        Config = new CryptoConfig
        {
            KeySize = "10001",
            PublicKey = priv.Modulus.ToString(16),
            PrivateKey = priv.Exponent.ToString(16),
            EnableServerToClientEncryption = true,
        };
    }

    private readonly CountingRsa _rsa = new(new RsaService(Options.Create(Config)));
    private readonly DiffieService _diffie;

    public DiffieHandshakeTests()
    {
        _diffie = new DiffieService(_rsa);
    }

    [Fact]
    public async Task Every_handshake_gets_the_same_signed_values_signed_once()
    {
        var inits = new List<ClientPacket>();
        var completes = new List<ClientPacket>();

        for (var i = 0; i < 3; i++)
        {
            var (harness, keys) = Connection();

            await harness.SendAsync(INIT, [], NO_PLAYER);
            var replies = await harness.SendAsync(COMPLETE, ClientKey(1234567 + i), NO_PLAYER);

            inits.Add(replies.Single(p => p.Header == INIT_REPLY));
            completes.Add(replies.Single(p => p.Header == COMPLETE_REPLY));
            keys.Should().ContainSingle("each connection exchanges keys once");
        }

        var prime = inits.Select(p => p.PopString()).ToList();
        var generator = inits.Select(p => p.PopString()).ToList();
        var serverKey = completes.Select(p => p.PopString()).ToList();

        prime.Should().AllBe(prime[0]);
        generator.Should().AllBe(generator[0]);
        serverKey.Should().AllBe(serverKey[0]);

        // What the Flash client checks before it answers: the values read back with the public
        // key, and the prime above the generator.
        Verified(prime[0]).CompareTo(Verified(generator[0])).Should().BePositive();

        _rsa.Signs.Should().Be(3, "the prime, the generator and the public key are signed once");
    }

    [Fact]
    public async Task A_second_complete_does_not_replace_the_session_keys()
    {
        var (harness, keys) = Connection();

        await harness.SendAsync(INIT, [], NO_PLAYER);
        await harness.SendAsync(COMPLETE, ClientKey(1234567), NO_PLAYER);
        var first = keys.Single();

        // Again before signing in, and again once signed in.
        await harness.SendAsync(COMPLETE, ClientKey(7654321), NO_PLAYER);
        var replies = await harness.SendAsync(COMPLETE, ClientKey(7654321), playerId: 5);

        keys.Should().Equal([first], "the session keeps the ciphers it set up");
        replies.Count(p => p.Header == COMPLETE_REPLY).Should().Be(1);
        harness.Fakes.Log.Of("CloseSessionAsync").Should().NotBeEmpty();
        _rsa.Decrypts.Should().Be(1, "a refused exchange does no private-key work");
    }

    [Fact]
    public async Task An_init_on_an_encrypted_session_closes_it()
    {
        var (harness, _) = Connection();

        await harness.SendAsync(INIT, [], NO_PLAYER);
        await harness.SendAsync(COMPLETE, ClientKey(1234567), NO_PLAYER);
        var replies = await harness.SendAsync(INIT, [], NO_PLAYER);

        replies.Count(p => p.Header == INIT_REPLY).Should().Be(1);
        harness.Fakes.Log.Of("CloseSessionAsync").Should().ContainSingle();
    }

    /// <summary>
    /// One connection: the server's Diffie service, and a session that holds a cipher once it is
    /// keyed, recording each key it is given.
    /// </summary>
    private (PacketHarness Harness, List<byte[]> Keys) Connection()
    {
        var harness = new PacketHarness();
        var keys = new List<byte[]>();

        harness.Resolver.Overrides[typeof(IDiffieService)] = _diffie;
        harness.Fakes.Handlers["SetupEncryption"] = call =>
        {
            keys.Add((byte[])call.Args[0]!);
            return null;
        };
        harness.Fakes.Handlers["get_CryptoIn"] = _ =>
            keys.Count == 0 ? null : new Rc4Engine(keys[0]);

        return (harness, keys);
    }

    /// <summary>The client's half of the exchange, encrypted with the server's public key.</summary>
    private static byte[] ClientKey(int value)
    {
        var cipher = new Pkcs1Encoding(new RsaEngine());
        cipher.Init(true, new ParametersWithRandom(PublicKey, new SecureRandom()));
        var plain = Encoding.UTF8.GetBytes(value.ToString());

        return PacketHarness.Payload(w =>
            w.String(Hex.ToHexString(cipher.ProcessBlock(plain, 0, plain.Length)))
        );
    }

    private static BigInteger Verified(string signed)
    {
        var cipher = new Pkcs1Encoding(new RsaEngine());
        cipher.Init(false, PublicKey);
        var bytes = Hex.Decode(signed);

        return new BigInteger(Encoding.UTF8.GetString(cipher.ProcessBlock(bytes, 0, bytes.Length)));
    }

    private sealed class CountingRsa(IRsaService inner) : IRsaService
    {
        public int Signs;
        public int Decrypts;

        public byte[] Encrypt(byte[] data) => inner.Encrypt(data);

        public byte[] Decrypt(byte[] data)
        {
            Interlocked.Increment(ref Decrypts);
            return inner.Decrypt(data);
        }

        public byte[] Sign(byte[] data)
        {
            Interlocked.Increment(ref Signs);
            return inner.Sign(data);
        }

        public bool Verify(byte[] data, byte[] signature) => inner.Verify(data, signature);
    }
}
