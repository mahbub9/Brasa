-- Creates the privileged, read-only, cross-tenant role background jobs and
-- migrations connect as (DAT-07).
--
-- Not a superuser and not BYPASSRLS -- both bypass row-level security
-- unconditionally, the exact mistake 01-app-role.sql was already written to
-- avoid for the ordinary runtime role (see ADR 0010). brasa_system is an
-- ordinary role, scoped entirely by its own explicit RLS policy
-- (RowLevelSecurity.EnableSystemReadFor, granted per table in the same
-- migration as the table itself) rather than by bypassing the security
-- system altogether. It is also read-only by construction: no table's
-- migration ever grants it INSERT/UPDATE/DELETE, so a bug in a policy
-- expression alone could never turn this into a write path.
--
-- Uses its own connection pool, not a role switch on brasa_app's connection
-- (see ModulePersistenceExtensions) -- Npgsql pools are keyed by the full
-- connection string, so different credentials can never share a pooled
-- physical connection. A tenant-scoped request can therefore never end up
-- running on a connection that happens to still be authenticated as
-- brasa_system, by construction, not by relying on connection-reset
-- behaviour on pool return.
--
-- Runs once, automatically, on first container init (docker-entrypoint-initdb.d
-- scripts do not re-run against an existing data volume).

DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'brasa_system') THEN
        CREATE ROLE brasa_system WITH LOGIN PASSWORD 'devonly_system';
    END IF;
END
$$;

GRANT CONNECT ON DATABASE brasa TO brasa_system;
