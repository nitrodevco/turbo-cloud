using System;

namespace Turbo.Core.Networking.Encryption;

public class KeyParameter(byte[] key) : ICipherParameters
{
    public byte[] Key { get; } = key ?? throw new ArgumentNullException(nameof(key));
}
