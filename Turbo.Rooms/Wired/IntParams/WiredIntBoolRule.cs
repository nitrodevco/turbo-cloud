namespace Turbo.Rooms.Wired.IntParams;

public sealed class WiredIntBoolRule(bool defaultValue)
    : WiredIntParamRule(defaultValue ? 1 : 0) { }
