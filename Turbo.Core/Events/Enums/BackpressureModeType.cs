namespace Turbo.Core.Events.Enums;

public enum BackpressureModeType
{
    Wait,
    DropOldest,
    DropNewest,
    Fail,
}
