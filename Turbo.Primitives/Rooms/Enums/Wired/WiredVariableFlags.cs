using System;
using System.Linq;

namespace Turbo.Primitives.Rooms.Enums.Wired;

[Flags]
public enum WiredVariableFlags
{
    None = 0,
    AlwaysAvailable = 1 << 0,
    CanCreateAndDelete = 1 << 1,
    HasValue = 1 << 2,
    CanWriteValue = 1 << 3,
    CanInterceptChanges = 1 << 4,
    IsInvisible = 1 << 5,
    CanReadCreationTime = 1 << 6,
    CanReadLastUpdateTime = 1 << 7,
    HasTextConnector = 1 << 8,
    IsStored = 1 << 9,
}

public static class WiredVariableFlagsExtensions
{
    public static WiredVariableFlags Add(
        this WiredVariableFlags current,
        WiredVariableFlags toAdd
    ) => current | toAdd;

    public static WiredVariableFlags Remove(
        this WiredVariableFlags current,
        WiredVariableFlags toRemove
    ) => current & ~toRemove;

    public static bool Has(this WiredVariableFlags current, WiredVariableFlags toCheck) =>
        (current & toCheck) != 0;

    public static bool Has(this WiredVariableFlags current, params WiredVariableFlags[] toCheck) =>
        toCheck.Any(flag => (current & flag) != 0);
}
