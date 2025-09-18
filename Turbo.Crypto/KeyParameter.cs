using System;

namespace Turbo.Crypto;

public class KeyParameter(byte[] key) : ICipherParameters
{
    public byte[] Key { get; } = key ?? throw new ArgumentNullException(nameof(key));
}
