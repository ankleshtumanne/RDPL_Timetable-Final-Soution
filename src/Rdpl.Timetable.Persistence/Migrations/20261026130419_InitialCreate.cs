using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Rdpl.Timetable.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "timetable");

            migrationBuilder.CreateTable(
                name: "sections",
                schema: "timetable",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    external_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    grade = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    section_name = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    curriculum_key = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    capacity = table.Column<int>(type: "integer", nullable: false),
                    is_pre_primary = table.Column<bool>(type: "boolean", nullable: false),
                    created_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sections", x => x.id);
                    table.CheckConstraint("ck_sections_capacity", "capacity >= 0");
                });

            migrationBuilder.CreateTable(
                name: "subjects",
                schema: "timetable",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_subjects", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "teachers",
                schema: "timetable",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_placeholder = table.Column<bool>(type: "boolean", nullable: false),
                    created_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_teachers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "timetable_runs",
                schema: "timetable",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    state = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    profile = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    outcome = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    success = table.Column<bool>(type: "boolean", nullable: true),
                    data_source_kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    uses_dummy_data = table.Column<bool>(type: "boolean", nullable: false),
                    data_source_warning = table.Column<string>(type: "text", nullable: true),
                    assignments_workbook = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: true),
                    solver_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    wall_time_ms = table.Column<long>(type: "bigint", nullable: true),
                    time_limit_seconds = table.Column<int>(type: "integer", nullable: false),
                    workers = table.Column<int>(type: "integer", nullable: false),
                    random_seed = table.Column<int>(type: "integer", nullable: false),
                    objective_value = table.Column<double>(type: "double precision", nullable: true),
                    hard_constraints_satisfied = table.Column<bool>(type: "boolean", nullable: true),
                    hard_violation_count = table.Column<int>(type: "integer", nullable: true),
                    soft_constraint_penalty = table.Column<long>(type: "bigint", nullable: true),
                    sections_in_scope = table.Column<int>(type: "integer", nullable: true),
                    sections_scheduled = table.Column<int>(type: "integer", nullable: true),
                    total_slots_scheduled = table.Column<int>(type: "integer", nullable: true),
                    error = table.Column<string>(type: "text", nullable: true),
                    report_json = table.Column<string>(type: "json", nullable: true),
                    solver_json = table.Column<string>(type: "json", nullable: true),
                    summary_json = table.Column<string>(type: "json", nullable: true),
                    diagnostic_json = table.Column<string>(type: "json", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_timetable_runs", x => x.id);
                    table.CheckConstraint("ck_timetable_runs_completed", "completed_utc IS NULL OR completed_utc >= created_utc");
                    table.CheckConstraint("ck_timetable_runs_time_limit", "time_limit_seconds BETWEEN 1 AND 1800");
                    table.CheckConstraint("ck_timetable_runs_wall_time", "wall_time_ms IS NULL OR wall_time_ms >= 0");
                    table.CheckConstraint("ck_timetable_runs_workers", "workers BETWEEN 1 AND 64");
                });

            migrationBuilder.CreateTable(
                name: "data_conflicts",
                schema: "timetable",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    severity = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    message = table.Column<string>(type: "text", nullable: false),
                    section_text = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    subject_text = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    teacher_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    source = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: true),
                    rule_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_data_conflicts", x => x.id);
                    table.ForeignKey(
                        name: "fk_data_conflicts_timetable_runs_run_id",
                        column: x => x.run_id,
                        principalSchema: "timetable",
                        principalTable: "timetable_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "scheduling_conflicts",
                schema: "timetable",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    rule_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    message = table.Column<string>(type: "text", nullable: false),
                    proves_infeasibility = table.Column<bool>(type: "boolean", nullable: false),
                    sections = table.Column<List<string>>(type: "text[]", nullable: false),
                    teacher_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_scheduling_conflicts", x => x.id);
                    table.ForeignKey(
                        name: "fk_scheduling_conflicts_timetable_runs_run_id",
                        column: x => x.run_id,
                        principalSchema: "timetable",
                        principalTable: "timetable_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "teaching_assignments",
                schema: "timetable",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    section_id = table.Column<int>(type: "integer", nullable: true),
                    subject_id = table.Column<int>(type: "integer", nullable: true),
                    teacher_id = table.Column<int>(type: "integer", nullable: true),
                    section_text = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    subject_text = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    teacher_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    teacher_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    workload_per_week = table.Column<int>(type: "integer", nullable: true),
                    source_file = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    source_sheet = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    source_row = table.Column<int>(type: "integer", nullable: false),
                    data_origin = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    is_dummy = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_teaching_assignments", x => x.id);
                    table.CheckConstraint("ck_teaching_assignments_source_row", "source_row >= 1");
                    table.ForeignKey(
                        name: "fk_teaching_assignments_sections_section_id",
                        column: x => x.section_id,
                        principalSchema: "timetable",
                        principalTable: "sections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_teaching_assignments_subjects_subject_id",
                        column: x => x.subject_id,
                        principalSchema: "timetable",
                        principalTable: "subjects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_teaching_assignments_teachers_teacher_id",
                        column: x => x.teacher_id,
                        principalSchema: "timetable",
                        principalTable: "teachers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_teaching_assignments_timetable_runs_run_id",
                        column: x => x.run_id,
                        principalSchema: "timetable",
                        principalTable: "timetable_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "timetable_entries",
                schema: "timetable",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    section_id = table.Column<int>(type: "integer", nullable: false),
                    subject_id = table.Column<int>(type: "integer", nullable: false),
                    teacher_id = table.Column<int>(type: "integer", nullable: true),
                    teacher_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    day = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    period = table.Column<short>(type: "smallint", nullable: false),
                    slot = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    end_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    lesson_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    instructors = table.Column<List<string>>(type: "text[]", nullable: false),
                    note = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_timetable_entries", x => x.id);
                    table.CheckConstraint("ck_timetable_entries_day", "day IN ('MONDAY','TUESDAY','WEDNESDAY','THURSDAY','FRIDAY','SATURDAY')");
                    table.CheckConstraint("ck_timetable_entries_lesson_type", "lesson_type IN ('SUBJECT','ACTIVITY','RESERVED_UNSTAFFED')");
                    table.CheckConstraint("ck_timetable_entries_period", "period BETWEEN 1 AND 12");
                    table.CheckConstraint("ck_timetable_entries_times", "end_time > start_time");
                    table.ForeignKey(
                        name: "fk_timetable_entries_sections_section_id",
                        column: x => x.section_id,
                        principalSchema: "timetable",
                        principalTable: "sections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_timetable_entries_subjects_subject_id",
                        column: x => x.subject_id,
                        principalSchema: "timetable",
                        principalTable: "subjects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_timetable_entries_teachers_teacher_id",
                        column: x => x.teacher_id,
                        principalSchema: "timetable",
                        principalTable: "teachers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_timetable_entries_timetable_runs_run_id",
                        column: x => x.run_id,
                        principalSchema: "timetable",
                        principalTable: "timetable_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_data_conflicts_run_id_type",
                schema: "timetable",
                table: "data_conflicts",
                columns: new[] { "run_id", "type" });

            migrationBuilder.CreateIndex(
                name: "ix_scheduling_conflicts_run_id_proves_infeasibility",
                schema: "timetable",
                table: "scheduling_conflicts",
                columns: new[] { "run_id", "proves_infeasibility" });

            migrationBuilder.CreateIndex(
                name: "ux_sections_display_name",
                schema: "timetable",
                table: "sections",
                column: "display_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_sections_external_id",
                schema: "timetable",
                table: "sections",
                column: "external_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_subjects_normalized_name",
                schema: "timetable",
                table: "subjects",
                column: "normalized_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_teachers_code",
                schema: "timetable",
                table: "teachers",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_teaching_assignments_run_id_teacher_id",
                schema: "timetable",
                table: "teaching_assignments",
                columns: new[] { "run_id", "teacher_id" });

            migrationBuilder.CreateIndex(
                name: "ix_teaching_assignments_section_id",
                schema: "timetable",
                table: "teaching_assignments",
                column: "section_id");

            migrationBuilder.CreateIndex(
                name: "ix_teaching_assignments_subject_id",
                schema: "timetable",
                table: "teaching_assignments",
                column: "subject_id");

            migrationBuilder.CreateIndex(
                name: "ix_teaching_assignments_teacher_id",
                schema: "timetable",
                table: "teaching_assignments",
                column: "teacher_id");

            migrationBuilder.CreateIndex(
                name: "ux_teaching_assignments_run_source_row",
                schema: "timetable",
                table: "teaching_assignments",
                columns: new[] { "run_id", "source_file", "source_sheet", "source_row" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_timetable_entries_run_id_subject_id",
                schema: "timetable",
                table: "timetable_entries",
                columns: new[] { "run_id", "subject_id" });

            migrationBuilder.CreateIndex(
                name: "ix_timetable_entries_section_id",
                schema: "timetable",
                table: "timetable_entries",
                column: "section_id");

            migrationBuilder.CreateIndex(
                name: "ix_timetable_entries_subject_id",
                schema: "timetable",
                table: "timetable_entries",
                column: "subject_id");

            migrationBuilder.CreateIndex(
                name: "ix_timetable_entries_teacher_id",
                schema: "timetable",
                table: "timetable_entries",
                column: "teacher_id");

            migrationBuilder.CreateIndex(
                name: "ux_timetable_entries_section_slot",
                schema: "timetable",
                table: "timetable_entries",
                columns: new[] { "run_id", "section_id", "day", "period" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_timetable_entries_teacher_slot",
                schema: "timetable",
                table: "timetable_entries",
                columns: new[] { "run_id", "teacher_id", "day", "period" },
                unique: true,
                filter: "teacher_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_timetable_runs_created_utc",
                schema: "timetable",
                table: "timetable_runs",
                column: "created_utc",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_timetable_runs_kind_state",
                schema: "timetable",
                table: "timetable_runs",
                columns: new[] { "kind", "state" });

            migrationBuilder.CreateIndex(
                name: "ix_timetable_runs_kind_state_completed_utc",
                schema: "timetable",
                table: "timetable_runs",
                columns: new[] { "kind", "state", "completed_utc" },
                descending: new[] { false, false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "data_conflicts",
                schema: "timetable");

            migrationBuilder.DropTable(
                name: "scheduling_conflicts",
                schema: "timetable");

            migrationBuilder.DropTable(
                name: "teaching_assignments",
                schema: "timetable");

            migrationBuilder.DropTable(
                name: "timetable_entries",
                schema: "timetable");

            migrationBuilder.DropTable(
                name: "sections",
                schema: "timetable");

            migrationBuilder.DropTable(
                name: "subjects",
                schema: "timetable");

            migrationBuilder.DropTable(
                name: "teachers",
                schema: "timetable");

            migrationBuilder.DropTable(
                name: "timetable_runs",
                schema: "timetable");
        }
    }
}
