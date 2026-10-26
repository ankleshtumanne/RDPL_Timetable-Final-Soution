# PostgreSQL persistence (optional)

Status: **Stage 3 done** — every endpoint is a controller backed by a service and a repository; `Program.cs` has no endpoints.
Persistence is **off by default** (`Persistence:Enabled=false`): the API then behaves exactly as before, and the CLI
never uses the database. Input data still comes from the client files; PostgreSQL only *stores* what a run used and
produced. It never replaces the file inputs or changes any scheduling rule.

## Architecture

```text
HTTP ─► Controllers/  TimetableController · SectionController · TeacherController · ConflictController · HealthController
          │  [ApiController] [Route("api/[controller]")] + the original /api/timetable/... routes as aliases
          ▼
        Services/Interfaces/  ITimetableService · ISectionService · ITeacherService · IConflictService
        Services/Implementations/
          │   TimetableService ──► IDatasetProvider → FileDatasetProvider → Core DatasetLoader (client FILES)
          │                    ├─► Core TimetableEngine (existing CP-SAT solver + independent validator, unchanged)
          │                    └─► ITimetableJobRunner → TimetableJobRunner (background solve, one at a time)
          │   SectionService / TeacherService / ConflictService ──► read stored rows, mapped by TimetableViewMapper
          ▼
        Repositories/Interfaces/  ITimetableRepository · ISectionRepository · ITeacherRepository · IConflictRepository
        Repositories/Implementations/
              EF Core (Persistence:Enabled=true): TimetableRepository · SectionRepository · TeacherRepository · ConflictRepository
                                                  ─► AppDbContext ─► PostgreSQL
              in-memory (default):               InMemoryRunStore + InMemory*Repository (original behaviour)
```

Everything is registered by dependency injection in `Infrastructure/ApiHostingExtensions.cs` (`AddTimetableStorage` →
`AddTimetablePersistence` or `AddInMemoryTimetableRepository`; `AddTimetableServices`; `AddControllers`). `Program.cs`
only calls these methods. `RunResultMapper` turns a Core report into repository records; the Persistence project never
references the solver.

| Stage | Content | State |
| --- | --- | --- |
| 1 | Persistence project, Npgsql EF Core provider, options, `AppDbContext`, 8 entities + configurations, `/health/ready` | done, verified |
| 2 | **TimetableController → TimetableService → TimetableRepository → PostgreSQL** (load-data, generate, jobs/{id}, result, runs), centralized exception handler, DTOs, schema script | **done, verified** |
| 3 | SectionController, TeacherController, ConflictController, HealthController (+ services + repositories); old `/api/timetable/sections`, `/teachers`, `/conflicts` kept as route aliases; `Program.cs` reduced to the composition root | **done, verified** |
| 4 | EF Core migrations (`InitialCreate`) replacing `02-create-schema.sql` | planned |

What is stored per run: run row (status, solver, validator verdict, dummy flag, report text), every input assignment
row, every generated lesson (only if the timetable passed the independent validator), every data and scheduling
conflict. Runs left RUNNING by a crash are marked FAILED at the next start (`RunRecoveryHostedService`).

## Tables (schema `timetable`)

| Table | Scope | Purpose |
| --- | --- | --- |
| `sections` | master, upsert by `external_id` | sections.json |
| `teachers` | master, upsert by `code` | employees from assignment rows (`is_placeholder` for UNASSIGNED-TT) |
| `subjects` | master, upsert by `normalized_name` | curriculum subjects and activity groups |
| `timetable_runs` | one row per load-data/generate | profile, solver status, wall time, validator verdict, `uses_dummy_data`, full report (`json`, kept byte-for-byte), solver/summary objects, `xmin` concurrency |
| `teaching_assignments` | per run | snapshot of every input row (raw text kept; FKs null when unresolved; `is_dummy`) |
| `timetable_entries` | per run | generated lessons — only for runs that passed the independent validator |
| `data_conflicts` | per run | `dataConflicts[]` (free text, no FKs: conflicts describe rows that may not exist) |
| `scheduling_conflicts` | per run | `schedulingConflicts[]` (`sections text[]`) |

Deleting a run cascades to its rows; master rows are `RESTRICT`ed. The database re-checks two hard rules as a last
line of defence: `ux_timetable_entries_section_slot` (H3) and `ux_timetable_entries_teacher_slot` filtered on
`teacher_id IS NOT NULL` (H1; activities are not teacher-booked, T6/AC8). Check constraints guard periods, days,
lesson types, time ranges and solver bounds.

## Database setup (run once)

```bash
# 1. Roles + database + schema (as a PostgreSQL superuser). Passwords are psql variables, never stored in files.
psql -h localhost -U postgres \
     -v owner_pw="'<strong-owner-password>'" -v app_pw="'<strong-app-password>'" \
     -f database/01-create-database.sql
# 2. Tables, constraints, indexes (as the schema owner)
psql -h localhost -U rdpl_owner -d rdpl_timetable -v ON_ERROR_STOP=1 -f database/02-create-schema.sql
```

`rdpl_owner` owns the schema and creates/changes tables. `rdpl_app` is what the API uses; it can only read/write
rows (verified: `DROP TABLE` → `must be owner`, `CREATE TABLE` → `permission denied`). `02-create-schema.sql` is
generated from `AppDbContext` and will be superseded by EF Core migrations (stage 4).

## Configuration — no secrets in source

```bash
cd src/Rdpl.Timetable.Api
# Development: user-secrets (stored outside the repo, in your user profile)
dotnet user-secrets set "ConnectionStrings:Timetable" "Host=localhost;Port=5432;Database=rdpl_timetable;Username=rdpl_app;Password=<app-password>"
dotnet user-secrets set "Api:ApiKey" "<at-least-32-characters>"
# enable for a run (or set "Persistence": { "Enabled": true } in appsettings.Development.json)
ASPNETCORE_ENVIRONMENT=Development Persistence__Enabled=true dotnet run
curl http://localhost:5000/health/ready     # Healthy
```

Production: environment variable `ConnectionStrings__Timetable`, or Azure Key Vault secret
`ConnectionStrings--Timetable` (build with `-p:EnableKeyVault=true`, set `KeyVault__Uri`; managed identity).
Add `SSL Mode=Require` (or `VerifyFull` with the server CA) for any non-local server.

Safety rules enforced in code:

* `Persistence:Enabled=true` without a connection string → the API refuses to start (fail closed) with setup instructions.
* Malformed strings, missing Host/Database/Username, or `Include Error Detail=true` → rejected; the message never echoes the value.
* Sensitive-data logging and detailed errors are forced off; `/health/ready` returns only `Healthy`/`Unhealthy`.
* Transient failures: Npgsql retry strategy (`MaxRetryCount`, default 3); command timeout 60 s.

## Tests

```bash
dotnet test                                    # model + configuration tests run everywhere
# real-database tests (create and drop their own database; use a disposable server):
export TIMETABLE_TEST_POSTGRES="Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=<admin-password>"
dotnet test --filter "FullyQualifiedName~Persistence"
```

Without `TIMETABLE_TEST_POSTGRES` the 7 database tests are reported as **skipped**, not passed.
