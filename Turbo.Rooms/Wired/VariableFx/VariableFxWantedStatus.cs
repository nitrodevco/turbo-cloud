using Turbo.Primitives.Rooms.Snapshots.Wired.VariableFx;

namespace Turbo.Rooms.Wired.VariableFx;

/// <summary>
/// A status a viewer should be seeing, with its <see cref="VariableFxBinding.SignatureOf"/>
/// worked out once for every viewer it is handed to rather than once per viewer. A status whose
/// value was written since the last flush is sent again even when it reads the same.
/// </summary>
internal readonly record struct VariableFxWantedStatus(
    VariableFxStatusSnapshot Status,
    string Signature,
    bool Written = false
);
