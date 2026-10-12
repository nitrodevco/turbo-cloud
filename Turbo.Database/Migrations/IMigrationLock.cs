using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;

namespace Turbo.Database.Migrations;

/// <summary>
/// One migrator at a time per database: two servers started together, or a server started while a
/// deploy script migrates, take turns instead of altering the same tables at once.
/// </summary>
public interface IMigrationLock
{
    /// <summary>
    /// Waits up to <paramref name="timeout"/> for the lock on <paramref name="connectionString"/>'s
    /// database; disposing the result gives it up.
    /// </summary>
    /// <exception cref="MigrationException">The lock stayed held, or the server cannot be reached.</exception>
    Task<IAsyncDisposable> AcquireAsync(
        string connectionString,
        TimeSpan timeout,
        CancellationToken ct
    );
}

/// <summary>
/// A PostgreSQL session advisory lock, held on a connection of its own to the server's
/// maintenance database (the hotel's may not exist yet). Unlike a lock row or table, it is
/// released by the server when that connection ends, so a migrator that is killed mid-way never
/// leaves the next start waiting on a lock nobody holds.
/// </summary>
public sealed class PostgresMigrationLock : IMigrationLock
{
    private const string PREFIX = "turbo:migrate:";
    private static readonly TimeSpan RETRY = TimeSpan.FromMilliseconds(250);

    public async Task<IAsyncDisposable> AcquireAsync(
        string connectionString,
        TimeSpan timeout,
        CancellationToken ct
    )
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        var key = KeyFor(builder.Database);

        // Pooling is off so that closing the connection really ends the session.
        builder.Database = "postgres";
        builder.Pooling = false;

        var connection = new NpgsqlConnection(builder.ConnectionString);

        try
        {
            await connection.OpenAsync(ct).ConfigureAwait(false);

            var deadline = DateTime.UtcNow + timeout;

            while (!await TryLockAsync(connection, key, ct).ConfigureAwait(false))
            {
                if (DateTime.UtcNow >= deadline)
                    throw new MigrationException(
                        $"Another process has been migrating this database for more than {timeout.TotalSeconds:0} seconds "
                            + "(a second Turbo, or a deploy script). Wait for it to finish, or find and stop it."
                    );

                await Task.Delay(RETRY, ct).ConfigureAwait(false);
            }
        }
        catch (NpgsqlException ex)
        {
            await connection.DisposeAsync().ConfigureAwait(false);

            throw new MigrationException(
                $"Cannot lock the database for migration: {ex.Message}",
                ex
            );
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);

            throw;
        }

        return new Held(connection, key);
    }

    /// <summary>The lock's key for a database: one per database name, as a 64-bit advisory key.</summary>
    public static long KeyFor(string? database) =>
        BitConverter.ToInt64(
            SHA256.HashData(Encoding.UTF8.GetBytes(PREFIX + (database ?? string.Empty))),
            0
        );

    private static async Task<bool> TryLockAsync(
        NpgsqlConnection connection,
        long key,
        CancellationToken ct
    )
    {
        var command = connection.CreateCommand();
        await using (command.ConfigureAwait(false))
        {
            command.CommandText = "SELECT pg_try_advisory_lock(@key)";
            command.Parameters.AddWithValue("key", key);

            return await command.ExecuteScalarAsync(ct).ConfigureAwait(false) is true;
        }
    }

    private sealed class Held(NpgsqlConnection connection, long key) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                var command = connection.CreateCommand();
                await using (command.ConfigureAwait(false))
                {
                    command.CommandText = "SELECT pg_advisory_unlock(@key)";
                    command.Parameters.AddWithValue("key", key);

                    await command.ExecuteScalarAsync().ConfigureAwait(false);
                }
            }
            catch (NpgsqlException)
            {
                // The connection is closed next, which releases the lock either way.
            }
            finally
            {
                await connection.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
