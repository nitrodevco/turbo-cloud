using Orleans;

namespace Turbo.Contracts.Shared;

[GenerateSerializer]
public record EnsureResult<T>(
    [property: Id(0)] EnsureStatus Status,
    [property: Id(1)] T? Value);