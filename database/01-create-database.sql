-- =====================================================================================================
-- RDPL Timetable — one-time PostgreSQL setup (PostgreSQL 13+; verified on 16).
-- Run as a superuser, e.g.:
--   psql -h localhost -U postgres -v owner_pw="'<owner-password>'" -v app_pw="'<app-password>'" -f database/01-create-database.sql
-- Passwords are passed as psql variables so they never live in this file or in the repository.
--
-- Two roles (least privilege):
--   rdpl_owner : owns the schema; used ONLY to apply EF Core migrations (Stage 3).
--   rdpl_app   : used by the Web API at runtime; data read/write only — cannot create, alter or drop tables.
-- =====================================================================================================
\set ON_ERROR_STOP on

CREATE ROLE rdpl_owner LOGIN PASSWORD :owner_pw;
CREATE ROLE rdpl_app   LOGIN PASSWORD :app_pw;

CREATE DATABASE rdpl_timetable OWNER rdpl_owner ENCODING 'UTF8' TEMPLATE template0;

\connect rdpl_timetable

REVOKE ALL ON DATABASE rdpl_timetable FROM PUBLIC;
GRANT CONNECT ON DATABASE rdpl_timetable TO rdpl_owner, rdpl_app;
REVOKE CREATE ON SCHEMA public FROM PUBLIC;

CREATE SCHEMA timetable AUTHORIZATION rdpl_owner;
GRANT USAGE ON SCHEMA timetable TO rdpl_app;

-- Tables and sequences that rdpl_owner creates later (migrations) are automatically usable by rdpl_app.
ALTER DEFAULT PRIVILEGES FOR ROLE rdpl_owner IN SCHEMA timetable GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO rdpl_app;
ALTER DEFAULT PRIVILEGES FOR ROLE rdpl_owner IN SCHEMA timetable GRANT USAGE, SELECT ON SEQUENCES TO rdpl_app;
