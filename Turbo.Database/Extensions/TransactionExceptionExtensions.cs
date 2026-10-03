using System;
using MySqlConnector;

namespace Turbo.Database.Extensions;

public static class TransactionExceptionExtensions
{
    /// <summary>A fresh transaction may retry a rolled-back MySQL deadlock or lock timeout.</summary>
    public static bool IsRetryableWriteConflict(this Exception exception) =>
        exception switch
        {
            MySqlException { Number: 1213 or 1205 } => true,
            { InnerException: { } inner } => inner.IsRetryableWriteConflict(),
            _ => false,
        };
}
