using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Turbo.Plugins.Configuration;

namespace Turbo.Plugins;

/// <summary>
/// Seals linked repositories' tokens with AES-GCM (nonce, tag, ciphertext, base64), as publish
/// targets' passwords are, so <c>plugins.json</c> alone does not give them away. The key is
/// <see cref="PluginConfig.SecretKey"/> when set, or else one made once beside the plugins as
/// <see cref="KEY_FILE"/>.
/// </summary>
internal sealed class PluginSecrets(IOptions<PluginConfig> config, ILogger<PluginSecrets> logger)
{
    public const string KEY_FILE = ".plugin-key";

    public const string UNREADABLE = "The saved token can't be read with this key; enter it again.";

    private const int KEY_BYTES = 32;
    private const int NONCE_BYTES = 12;
    private const int TAG_BYTES = 16;

    private readonly Lazy<byte[]> _key = new(
        () => LoadKey(config.Value, logger),
        LazyThreadSafetyMode.ExecutionAndPublication
    );

    public string Seal(string secret)
    {
        var plain = Encoding.UTF8.GetBytes(secret);
        var sealedBytes = new byte[NONCE_BYTES + TAG_BYTES + plain.Length];
        var nonce = sealedBytes.AsSpan(0, NONCE_BYTES);

        RandomNumberGenerator.Fill(nonce);

        using var aes = new AesGcm(_key.Value, TAG_BYTES);

        aes.Encrypt(
            nonce,
            plain,
            sealedBytes.AsSpan(NONCE_BYTES + TAG_BYTES),
            sealedBytes.AsSpan(NONCE_BYTES, TAG_BYTES)
        );

        return Convert.ToBase64String(sealedBytes);
    }

    /// <summary>The secret; throws <see cref="InvalidOperationException"/> (<see cref="UNREADABLE"/>) when it was sealed with another key.</summary>
    public string Unseal(string sealedText)
    {
        try
        {
            var sealedBytes = Convert.FromBase64String(sealedText);

            if (sealedBytes.Length < NONCE_BYTES + TAG_BYTES)
                throw new InvalidOperationException(UNREADABLE);

            var plain = new byte[sealedBytes.Length - NONCE_BYTES - TAG_BYTES];

            using var aes = new AesGcm(_key.Value, TAG_BYTES);

            aes.Decrypt(
                sealedBytes.AsSpan(0, NONCE_BYTES),
                sealedBytes.AsSpan(NONCE_BYTES + TAG_BYTES),
                sealedBytes.AsSpan(NONCE_BYTES, TAG_BYTES),
                plain
            );

            return Encoding.UTF8.GetString(plain);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            logger.LogWarning("A linked repository's saved token did not unseal with the key");

            throw new InvalidOperationException(UNREADABLE, ex);
        }
    }

    private static byte[] LoadKey(PluginConfig config, ILogger logger)
    {
        if (!string.IsNullOrWhiteSpace(config.SecretKey))
            return Decode(config.SecretKey, "Turbo:Plugin:SecretKey");

        var path = Path.Combine(config.PluginFolderPath, KEY_FILE);

        if (File.Exists(path))
            return Decode(File.ReadAllText(path), path);

        Directory.CreateDirectory(config.PluginFolderPath);

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
        catch (IOException) when (File.Exists(path))
        {
            // Another server sharing the folder made it first: theirs is the key.
            return Decode(File.ReadAllText(path), path);
        }

        logger.LogInformation("Made the plugin key {Path} for sealing tokens", path);

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
            : throw new InvalidOperationException($"{where} is not a {KEY_BYTES}-byte key.");
    }
}
