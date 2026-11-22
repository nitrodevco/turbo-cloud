using System;
using MySqlConnector;

namespace Turbo.Database.Extensions;

public static class TransactionExceptionExtensions
{
    /// <summary>
    /// A fresh transaction may retry a write that lost to another one: a deadlock, a lock wait
    /// timeout, or MariaDB 11.6+ refusing to lock or change a row another transaction committed
    /// after this one's snapshot (<c>ER_CHECKREAD</c> under <c>innodb_snapshot_isolation</c>, on
    /// by default), where MySQL would have handed it the newer row.
    /// </summary>
    public static bool IsRetryableWriteConflict(this Exception exception) =>
        exception switch
        {
            MySqlException
            {
                ErrorCode: MySqlErrorCode.LockDeadlock
                    or MySqlErrorCode.LockWaitTimeout
                    or MySqlErrorCode.CheckRead,
            } => true,
            { InnerException: { } inner } => inner.IsRetryableWriteConflict(),
            _ => false,
        };
}
