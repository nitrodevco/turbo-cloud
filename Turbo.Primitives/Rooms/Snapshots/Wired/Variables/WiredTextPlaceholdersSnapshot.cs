using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Rooms.Snapshots.Wired.Variables;

/// <summary>
/// Wired context section 7 (<c>wiredcontext/params/TextPlaceholders</c>): the placeholder names a
/// message editor's "Add placeholder" button lists, each inserted as <c>$(name)</c>, then the
/// names of the stack's capturers.
/// </summary>
[GenerateSerializer, Immutable]
public record WiredTextPlaceholdersSnapshot : WiredVariableContextSnapshot
{
    [Id(1)]
    public required ImmutableArray<string> PlaceholderNames { get; init; }

    [Id(2)]
    public required ImmutableArray<string> CapturerNames { get; init; }
}
