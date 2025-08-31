namespace Turbo.Events.Abstractions.Enums;

public enum BackpressureModeType
{
    Wait,
    DropOldest,
    DropNewest,
    Fail,
}
