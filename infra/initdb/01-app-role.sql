-- Creates the restricted role the application connects as at runtime.
--
-- The bootstrap role from POSTGRES_USER (see docker-compose.yml) is a
-- PostgreSQL SUPERUSER. Superusers bypass row-level security unconditionally —
-- FORCE ROW LEVEL SECURITY only changes behaviour for the table OWNER, never for
-- a superuser. Running the app as that role would make every RLS policy in
-- every migration a no-op while looking, from the SQL alone, perfectly correct.
--
-- brasa_app is an ordinary role: no SUPERUSER, no BYPASSRLS. Table-level
-- privileges are granted per table by RowLevelSecurity.EnableFor, in the same
-- migration that creates the table and its policy — see
-- src/backend/Brasa.Shared/Persistence/RowLevelSecurity.cs and
-- docs/architecture/multi-tenancy.md.
--
-- Runs once, automatically, on first container init (docker-entrypoint-initdb.d
-- scripts do not re-run against an existing data volume).

DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'brasa_app') THEN
        CREATE ROLE brasa_app WITH LOGIN PASSWORD 'devonly_app';
    END IF;
END
$$;

GRANT CONNECT ON DATABASE brasa TO brasa_app;
