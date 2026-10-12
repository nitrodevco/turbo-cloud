-- A role that owns the database and migrates it, and one the server runs as, which reads and
-- writes rows and nothing else. Run once as a superuser (sudo -u postgres psql -f this-file),
-- after replacing the two passwords, before the first deploy. See docs/database.md,
-- "A role for the server, and one for migrations".
--
-- Turbo:Database:ConnectionString          -> turbo_app
-- Turbo:Database:MigrationConnectionString -> turbo_owner

CREATE ROLE turbo_owner LOGIN PASSWORD 'replace with a long random password';
CREATE ROLE turbo_app LOGIN PASSWORD 'replace with another long random password';

CREATE DATABASE turbo OWNER turbo_owner;
REVOKE ALL ON DATABASE turbo FROM PUBLIC;
GRANT CONNECT ON DATABASE turbo TO turbo_owner, turbo_app;

\connect turbo

-- Nobody but the owner creates anything in the schema.
REVOKE CREATE ON SCHEMA public FROM PUBLIC;
ALTER SCHEMA public OWNER TO turbo_owner;
GRANT USAGE ON SCHEMA public TO turbo_app;

-- Every table and sequence the owner makes (every migration's, every plugin's) is the server's
-- to read and write, and only that: no DDL, no TRUNCATE, no triggers of its own.
ALTER DEFAULT PRIVILEGES FOR ROLE turbo_owner IN SCHEMA public
  GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO turbo_app;
ALTER DEFAULT PRIVILEGES FOR ROLE turbo_owner IN SCHEMA public
  GRANT USAGE, SELECT ON SEQUENCES TO turbo_app;
-- And whatever is there already, for a database migrated before this was run.
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO turbo_app;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO turbo_app;

-- Limits on the server's own sessions, so one bug can't hold the hotel up:
-- * a transaction left open (a crash between two writes) gives its row locks back after a minute;
-- * a statement that waits on a lock gives up after ten seconds and the action is retried or refused;
-- * a statement that runs away is stopped after a minute.
-- Migrations run as turbo_owner and are not limited by these.
ALTER ROLE turbo_app SET idle_in_transaction_session_timeout = '60s';
ALTER ROLE turbo_app SET lock_timeout = '10s';
ALTER ROLE turbo_app SET statement_timeout = '60s';
