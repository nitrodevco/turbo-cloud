using System.Collections.Generic;
using Turbo.Primitives.Rooms.Enums.Wired;

namespace Turbo.Rooms.Wired;

/// <summary>
/// A trigger that names a user of its own beside the triggering one, which the boxes stacked
/// with it can then act on: "User clicks User" gives "The clicked user". The client offers what
/// the server lists for a box, so those boxes list it after the triggering user.
/// </summary>
public interface IWiredUserSourceProvider
{
    public IReadOnlyList<WiredPlayerSourceType> ProvidedUserSources { get; }
}
