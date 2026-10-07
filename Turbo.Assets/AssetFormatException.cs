using System;

namespace Turbo.Assets;

/// <summary>A file that is not the asset library it claims to be, or is one this reader can't open.</summary>
public sealed class AssetFormatException : Exception
{
    public AssetFormatException(string message)
        : base(message) { }

    public AssetFormatException(string message, Exception inner)
        : base(message, inner) { }
}
