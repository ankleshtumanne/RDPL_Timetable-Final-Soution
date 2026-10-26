-- =====================================================================================================
-- RDPL Timetable — schema (tables, constraints, indexes) for the "timetable" schema.
-- GENERATED from AppDbContext with Database.GenerateCreateScript(); do not edit by hand.
-- Run ONCE as rdpl_owner after 01-create-database.sql:
--   psql -h localhost -U rdpl_owner -d rdpl_timetable -v ON_ERROR_STOP=1 -f database/02-create-schema.sql
-- Default privileges from 01-create-database.sql give rdpl_app read/write access to every table created here.
-- EF Core migrations (a later stage) will replace this script; for a database created from it, the initial
-- migration is marked as applied instead of re-run (documented in docs/DATABASE.md).
-- =====================================================================================================
DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'timetable') THEN
        CREATE SCHEMA timetable;
    END IF;
END $EF$;


CREATE TABLE timetable.sections (
    id integer GENERATED ALWAYS AS IDENTITY,
    external_id character varying(64) NOT NULL,
    display_name character varying(200) NOT NULL,
    grade character varying(32) NOT NULL,
    section_name character varying(32) NOT NULL,
    curriculum_key character varying(32) NOT NULL,
    capacity integer NOT NULL,
    is_pre_primary boolean NOT NULL,
    created_utc timestamp with time zone NOT NULL,
    updated_utc timestamp with time zone NOT NULL,
    CONSTRAINT pk_sections PRIMARY KEY (id),
    CONSTRAINT ck_sections_capacity CHECK (capacity >= 0)
);


CREATE TABLE timetable.subjects (
    id integer GENERATED ALWAYS AS IDENTITY,
    name character varying(200) NOT NULL,
    normalized_name character varying(200) NOT NULL,
    kind character varying(32) NOT NULL,
    created_utc timestamp with time zone NOT NULL,
    updated_utc timestamp with time zone NOT NULL,
    CONSTRAINT pk_subjects PRIMARY KEY (id)
);


CREATE TABLE timetable.teachers (
    id integer GENERATED ALWAYS AS IDENTITY,
    code character varying(64) NOT NULL,
    name character varying(200) NOT NULL,
    is_placeholder boolean NOT NULL,
    created_utc timestamp with time zone NOT NULL,
    updated_utc timestamp with time zone NOT NULL,
    CONSTRAINT pk_teachers PRIMARY KEY (id)
);


CREATE TABLE timetable.timetable_runs (
    id uuid NOT NULL,
    kind character varying(32) NOT NULL,
    state character varying(32) NOT NULL,
    profile character varying(32) NOT NULL,
    created_utc timestamp with time zone NOT NULL,
    completed_utc timestamp with time zone,
    outcome character varying(64),
    success boolean,
    data_source_kind character varying(64) NOT NULL,
    uses_dummy_data boolean NOT NULL,
    data_source_warning text,
    assignments_workbook character varying(260),
    solver_status character varying(32),
    wall_time_ms bigint,
    time_limit_seconds integer NOT NULL,
    workers integer NOT NULL,
    random_seed integer NOT NULL,
    objective_value double precision,
    hard_constraints_satisfied boolean,
    hard_violation_count integer,
    soft_constraint_penalty bigint,
    sections_in_scope integer,
    sections_scheduled integer,
    total_slots_scheduled integer,
    error text,
    report_json json,
    solver_json json,
    summary_json json,
    diagnostic_json json,
    CONSTRAINT pk_timetable_runs PRIMARY KEY (id),
    CONSTRAINT ck_timetable_runs_completed CHECK (completed_utc IS NULL OR completed_utc >= created_utc),
    CONSTRAINT ck_timetable_runs_time_limit CHECK (time_limit_seconds BETWEEN 1 AND 1800),
    CONSTRAINT ck_timetable_runs_wall_time CHECK (wall_time_ms IS NULL OR wall_time_ms >= 0),
    CONSTRAINT ck_timetable_runs_workers CHECK (workers BETWEEN 1 AND 64)
);


CREATE TABLE timetable.data_conflicts (
    id bigint GENERATED ALWAYS AS IDENTITY,
    run_id uuid NOT NULL,
    type character varying(64) NOT NULL,
    severity character varying(16) NOT NULL,
    message text NOT NULL,
    section_text character varying(200),
    subject_text character varying(200),
    teacher_code character varying(64),
    source character varying(260),
    rule_id character varying(32),
    CONSTRAINT pk_data_conflicts PRIMARY KEY (id),
    CONSTRAINT fk_data_conflicts_timetable_runs_run_id FOREIGN KEY (run_id) REFERENCES timetable.timetable_runs (id) ON DELETE CASCADE
);


CREATE TABLE timetable.scheduling_conflicts (
    id bigint GENERATED ALWAYS AS IDENTITY,
    run_id uuid NOT NULL,
    code character varying(64) NOT NULL,
    rule_id character varying(64) NOT NULL,
    message text NOT NULL,
    proves_infeasibility boolean NOT NULL,
    sections text[] NOT NULL,
    teacher_code character varying(64),
    CONSTRAINT pk_scheduling_conflicts PRIMARY KEY (id),
    CONSTRAINT fk_scheduling_conflicts_timetable_runs_run_id FOREIGN KEY (run_id) REFERENCES timetable.timetable_runs (id) ON DELETE CASCADE
);


CREATE TABLE timetable.teaching_assignments (
    id bigint GENERATED ALWAYS AS IDENTITY,
    run_id uuid NOT NULL,
    section_id integer,
    subject_id integer,
    teacher_id integer,
    section_text character varying(200) NOT NULL,
    subject_text character varying(200) NOT NULL,
    teacher_code character varying(64) NOT NULL,
    teacher_name character varying(200) NOT NULL,
    workload_per_week integer,
    source_file character varying(260) NOT NULL,
    source_sheet character varying(64) NOT NULL,
    source_row integer NOT NULL,
    data_origin character varying(64) NOT NULL,
    is_dummy boolean NOT NULL,
    CONSTRAINT pk_teaching_assignments PRIMARY KEY (id),
    CONSTRAINT ck_teaching_assignments_source_row CHECK (source_row >= 1),
    CONSTRAINT fk_teaching_assignments_sections_section_id FOREIGN KEY (section_id) REFERENCES timetable.sections (id) ON DELETE RESTRICT,
    CONSTRAINT fk_teaching_assignments_subjects_subject_id FOREIGN KEY (subject_id) REFERENCES timetable.subjects (id) ON DELETE RESTRICT,
    CONSTRAINT fk_teaching_assignments_teachers_teacher_id FOREIGN KEY (teacher_id) REFERENCES timetable.teachers (id) ON DELETE RESTRICT,
    CONSTRAINT fk_teaching_assignments_timetable_runs_run_id FOREIGN KEY (run_id) REFERENCES timetable.timetable_runs (id) ON DELETE CASCADE
);


CREATE TABLE timetable.timetable_entries (
    id bigint GENERATED ALWAYS AS IDENTITY,
    run_id uuid NOT NULL,
    section_id integer NOT NULL,
    subject_id integer NOT NULL,
    teacher_id integer,
    teacher_name character varying(200),
    day character varying(16) NOT NULL,
    period smallint NOT NULL,
    slot character varying(16) NOT NULL,
    start_time time without time zone NOT NULL,
    end_time time without time zone NOT NULL,
    lesson_type character varying(32) NOT NULL,
    instructors text[] NOT NULL,
    note text,
    CONSTRAINT pk_timetable_entries PRIMARY KEY (id),
    CONSTRAINT ck_timetable_entries_day CHECK (day IN ('MONDAY','TUESDAY','WEDNESDAY','THURSDAY','FRIDAY','SATURDAY')),
    CONSTRAINT ck_timetable_entries_lesson_type CHECK (lesson_type IN ('SUBJECT','ACTIVITY','RESERVED_UNSTAFFED')),
    CONSTRAINT ck_timetable_entries_period CHECK (period BETWEEN 1 AND 12),
    CONSTRAINT ck_timetable_entries_times CHECK (end_time > start_time),
    CONSTRAINT fk_timetable_entries_sections_section_id FOREIGN KEY (section_id) REFERENCES timetable.sections (id) ON DELETE RESTRICT,
    CONSTRAINT fk_timetable_entries_subjects_subject_id FOREIGN KEY (subject_id) REFERENCES timetable.subjects (id) ON DELETE RESTRICT,
    CONSTRAINT fk_timetable_entries_teachers_teacher_id FOREIGN KEY (teacher_id) REFERENCES timetable.teachers (id) ON DELETE RESTRICT,
    CONSTRAINT fk_timetable_entries_timetable_runs_run_id FOREIGN KEY (run_id) REFERENCES timetable.timetable_runs (id) ON DELETE CASCADE
);


CREATE INDEX ix_data_conflicts_run_id_type ON timetable.data_conflicts (run_id, type);


CREATE INDEX ix_scheduling_conflicts_run_id_proves_infeasibility ON timetable.scheduling_conflicts (run_id, proves_infeasibility);


CREATE UNIQUE INDEX ux_sections_display_name ON timetable.sections (display_name);


CREATE UNIQUE INDEX ux_sections_external_id ON timetable.sections (external_id);


CREATE UNIQUE INDEX ux_subjects_normalized_name ON timetable.subjects (normalized_name);


CREATE UNIQUE INDEX ux_teachers_code ON timetable.teachers (code);


CREATE INDEX ix_teaching_assignments_run_id_teacher_id ON timetable.teaching_assignments (run_id, teacher_id);


CREATE INDEX ix_teaching_assignments_section_id ON timetable.teaching_assignments (section_id);


CREATE INDEX ix_teaching_assignments_subject_id ON timetable.teaching_assignments (subject_id);


CREATE INDEX ix_teaching_assignments_teacher_id ON timetable.teaching_assignments (teacher_id);


CREATE UNIQUE INDEX ux_teaching_assignments_run_source_row ON timetable.teaching_assignments (run_id, source_file, source_sheet, source_row);


CREATE INDEX ix_timetable_entries_run_id_subject_id ON timetable.timetable_entries (run_id, subject_id);


CREATE INDEX ix_timetable_entries_section_id ON timetable.timetable_entries (section_id);


CREATE INDEX ix_timetable_entries_subject_id ON timetable.timetable_entries (subject_id);


CREATE INDEX ix_timetable_entries_teacher_id ON timetable.timetable_entries (teacher_id);


CREATE UNIQUE INDEX ux_timetable_entries_section_slot ON timetable.timetable_entries (run_id, section_id, day, period);


CREATE UNIQUE INDEX ux_timetable_entries_teacher_slot ON timetable.timetable_entries (run_id, teacher_id, day, period) WHERE teacher_id IS NOT NULL;


CREATE INDEX ix_timetable_runs_created_utc ON timetable.timetable_runs (created_utc DESC);


CREATE INDEX ix_timetable_runs_kind_state ON timetable.timetable_runs (kind, state);


CREATE INDEX ix_timetable_runs_kind_state_completed_utc ON timetable.timetable_runs (kind, state, completed_utc DESC);


