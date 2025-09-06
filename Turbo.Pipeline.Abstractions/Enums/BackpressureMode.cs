namespace Turbo.Pipeline.Abstractions.Enums;

public enum BackpressureMode
{
    Wait,
    DropOldest,
    DropNewest,
    Fail,
}
