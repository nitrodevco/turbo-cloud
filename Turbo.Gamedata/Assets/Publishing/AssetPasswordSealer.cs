using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Turbo.Gamedata.Configuration;
using Turbo.Primitives.Gamedata;

namespace Turbo.Gamedata.Assets.Publishing;

/// <summary>
/// Seals publish targets' passwords with AES-GCM, laid out as nonce, tag and ciphertext, so a
/// database dump alone does not give them away. The key is <see cref="AssetBundleConfig.SecretKey"/>
/// when set, or else one made once and kept in the bundle folder as <see cref="KEY_FILE"/>.
/// </summary>
internal sealed class AssetPasswordSealer(
    IOptions<AssetBundleConfig> config,
    IAssetBundleStore store,
    ILogger<AssetPasswordSealer> logger
)
{
    public const string KEY_FILE = ".publish-key";

    public const string UNREADABLE =
        "The saved password can't be read with this key; enter it again.";

    private const int KEY_BYTES = 32;
    private const int NONCE_BYTES = 12;
    private const int TAG_BYTES = 16;

    private readonly Lazy<byte[]> _key = new(
        () => LoadKey(config.Value.SecretKey, store.Root, logger),
        LazyThreadSafetyMode.ExecutionAndPublication
    );

    public byte[] Seal(string password)
    {
        var plain = Encoding.UTF8.GetBytes(password);
        var sealedBytes = new byte[NONCE_BYTES + TAG_BYTES + plain.Length];
        var nonce = sealedBytes.AsSpan(0, NONCE_BYTES);
        var tag = sealedBytes.AsSpan(NONCE_BYTES, TAG_BYTES);
        var cipher = sealedBytes.AsSpan(NONCE_BYTES + TAG_BYTES);

        RandomNumberGenerator.Fill(nonce);

        using var aes = new AesGcm(_key.Value, TAG_BYTES);

        aes.Encrypt(nonce, plain, cipher, tag);

        return sealedBytes;
    }

    /// <summary>
    /// The password; throws <see cref="InvalidOperationException"/> with <see cref="UNREADABLE"/>
    /// when it was sealed with another key, or is not a sealed password at all.
    /// </summary>
    public string Unseal(byte[] sealedBytes)
    {
        if (sealedBytes.Length < NONCE_BYTES + TAG_BYTES)
            throw new InvalidOperationException(UNREADABLE);

        var nonce = sealedBytes.AsSpan(0, NONCE_BYTES);
        var tag = sealedBytes.AsSpan(NONCE_BYTES, TAG_BYTES);
        var cipher = sealedBytes.AsSpan(NONCE_BYTES + TAG_BYTES);
        var plain = new byte[cipher.Length];

        try
        {
            using var aes = new AesGcm(_key.Value, TAG_BYTES);

            aes.Decrypt(nonce, cipher, tag, plain);
        }
        catch (CryptographicException ex)
        {
            logger.LogWarning(ex, "A publish target's saved password did not unseal with the key");

            throw new InvalidOperationException(UNREADABLE, ex);
        }

        return Encoding.UTF8.GetString(plain);
    }

    private static byte[] LoadKey(string configured, string root, ILogger logger)
    {
        if (!string.IsNullOrWhiteSpace(configured))
            return Decode(configured, "Turbo:Assets:SecretKey");

        var path = Path.Combine(root, KEY_FILE);

        if (File.Exists(path))
            return Decode(File.ReadAllText(path), path);

        Directory.CreateDirectory(root);

        var key = RandomNumberGenerator.GetBytes(KEY_BYTES);
        var options = new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            Share = FileShare.None,
        };

        if (!OperatingSystem.IsWindows())
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;

        try
        {
            using var writer = new StreamWriter(path, Encoding.ASCII, options);

            writer.Write(Convert.ToBase64String(key));
        }
        catch (IOException ex) when (File.Exists(path))
        {
            // Another server sharing the folder made it first: theirs is the key.
            logger.LogInformation(ex, "The publish key {Path} was made elsewhere; using it", path);

            return Decode(File.ReadAllText(path), path);
        }

        logger.LogInformation("Made the publish key {Path} for sealing passwords", path);

        return key;
    }

    private static byte[] Decode(string base64, string where)
    {
        byte[] key;

        try
        {
            key = Convert.FromBase64String(base64.Trim());
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException($"{where} is not base64.", ex);
        }

        return key.Length == KEY_BYTES
            ? key
            : throw new InvalidOperationException(
                $"{where} must be {KEY_BYTES} bytes as base64, not {key.Length}."
            );
    }
}
