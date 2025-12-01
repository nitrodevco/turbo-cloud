using System;
using Turbo.Primitives;

namespace Turbo.Logging;

public sealed class TurboException : Exception
{
    public TurboErrorCodeEnum ErrorCode { get; }

    public TurboException(TurboErrorCodeEnum code, string? message = null, Exception? inner = null)
        : base(message ?? code.ToDefaultMessage(), inner)
    {
        ErrorCode = code;
    }
}
