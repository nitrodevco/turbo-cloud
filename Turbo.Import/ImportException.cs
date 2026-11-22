using System;

namespace Turbo.Import;

/// <summary>An import that cannot go on, for a reason an operator can act on; the message says it.</summary>
public sealed class ImportException : Exception
{
    public ImportException(string message)
        : base(message) { }

    public ImportException(string message, Exception inner)
        : base(message, inner) { }
}
