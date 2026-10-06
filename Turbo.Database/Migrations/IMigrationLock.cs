using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MySqlConnector;

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
/// MySQL's and MariaDB's named lock (<c>GET_LOCK</c>), held on a connection of its own. Unlike a
/// lock row or table, it is released by the server when that connection ends, so a migrator that
/// is killed mid-way never leaves the next start waiting on a lock nobody holds.
/// </summary>
public sealed class MySqlMigrationLock : IMigrationLock
{
    private const int MAX_NAME_LENGTH = 64;
    private const string PREFIX = "turbo:migrate:";

    public async Task<IAsyncDisposable> AcquireAsync(
        string connectionString,
        TimeSpan timeout,
        CancellationToken ct
    )
    {
        var builder = new MySqlConnectionStringBuilder(connectionString);
        var name = NameFor(builder.Database);

        // Named locks belong to the server, not a schema, and the database may not exist yet.
        // Pooling is off so that closing the connection really ends the session.
        builder.Database = string.Empty;
        builder.Pooling = false;

        var connection = new MySqlConnection(builder.ConnectionString);

        try
        {
            await connection.OpenAsync(ct).ConfigureAwait(false);

            var command = connection.CreateCommand();
            await using (command.ConfigureAwait(false))
            {
                command.CommandText = "SELECT GET_LOCK(@name, @seconds)";
                command.Parameters.AddWithValue("@name", name);
                command.Parameters.AddWithValue(
                    "@seconds",
                    (int)Math.Ceiling(timeout.TotalSeconds)
                );
                command.CommandTimeout = (int)timeout.TotalSeconds + 30;

                var answer = await command.ExecuteScalarAsync(ct).ConfigureAwait(false);

                if (answer is DBNull or null || Convert.ToInt64(answer) != 1)
                    throw new MigrationException(
                        $"Another process has been migrating this database for more than {timeout.TotalSeconds:0} seconds "
                            + "(a second Turbo, or a deploy script). Wait for it to finish, or find and stop it."
                    );
            }
        }
        catch (MySqlException ex)
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

        return new Held(connection, name);
    }

    /// <summary>The lock's name for a schema: one per database, within MySQL's 64 character limit.</summary>
    public static string NameFor(string? database)
    {
        var name = PREFIX + (database ?? string.Empty);

        return name.Length <= MAX_NAME_LENGTH
            ? name
            : PREFIX + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(name)))[..32];
    }

    private sealed class Held(MySqlConnection connection, string name) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                var command = connection.CreateCommand();
                await using (command.ConfigureAwait(false))
                {
                    command.CommandText = "SELECT RELEASE_LOCK(@name)";
                    command.Parameters.AddWithValue("@name", name);

                    await command.ExecuteScalarAsync().ConfigureAwait(false);
                }
            }
            catch (MySqlException)
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
