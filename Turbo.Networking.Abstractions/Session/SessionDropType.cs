namespace Turbo.Networking.Abstractions.Session;

public enum SessionDropType
{
    Wait,
    DropOldest,
    DropNonCritical,
}
