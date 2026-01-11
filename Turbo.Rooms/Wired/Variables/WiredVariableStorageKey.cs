namespace Turbo.Rooms.Wired.Variables;

public sealed class WiredVariableStorageKey
{
    public required int RoomId { get; init; }
    public required string Key { get; init; }
    public required WiredVariableBinding Binding { get; init; }

    public string GetBuildKey() => $"{RoomId}:{Key}:{Binding.Target}:{Binding.TargetId}";
}
