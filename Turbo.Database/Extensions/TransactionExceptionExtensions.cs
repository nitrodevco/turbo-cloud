using System;
using Npgsql;

namespace Turbo.Database.Extensions;

public static class TransactionExceptionExtensions
{
    /// <summary>
    /// A fresh transaction may retry a write PostgreSQL rolled back over a conflict: a deadlock
    /// (40P01), a lock it could not get in time (55P03), or a serialization failure (40001).
    /// </summary>
    public static bool IsRetryableWriteConflict(this Exception exception) =>
        exception switch
        {
            PostgresException
            {
                SqlState: PostgresErrorCodes.DeadlockDetected
                    or PostgresErrorCodes.LockNotAvailable
                    or PostgresErrorCodes.SerializationFailure,
            } => true,
            { InnerException: { } inner } => inner.IsRetryableWriteConflict(),
            _ => false,
        };
}
