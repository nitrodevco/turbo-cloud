namespace Turbo.Rooms.Wired;

internal sealed class WiredErrorLogEntry
{
    public required int Id { get; init; }
    public required string ErrorName { get; init; }
    public required string Category { get; init; }
    public int ThrowCount { get; set; }
    public long LastOccurrenceMs { get; set; }
}
