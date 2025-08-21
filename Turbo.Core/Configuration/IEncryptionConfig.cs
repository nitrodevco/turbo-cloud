using System.Collections;
using System.Collections.Generic;

namespace Turbo.Core.Configuration;

public class IEncryptionConfig
{
    public string KeySize { get; init; }
    public string PublicKey { get; init; }
    public string PrivateKey { get; init; }
}
