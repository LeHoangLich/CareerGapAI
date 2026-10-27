using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareerGap.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class CvWritingProposals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_job_type",
                table: "ai_jobs");

            migrationBuilder.CreateTable(
                name: "writing_inputs",
                columns: table => new
                {
                    ai_job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    resume_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    resume_revision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fingerprint = table.Column<string>(type: "char(64)", nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_writing_inputs", x => x.ai_job_id);
                    table.ForeignKey(
                        name: "FK_writing_inputs_ai_jobs_ai_job_id",
                        column: x => x.ai_job_id,
                        principalTable: "ai_jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_writing_inputs_resume_data_revisions_resume_revision_id_res~",
                        columns: x => new { x.resume_revision_id, x.resume_version_id },
                        principalTable: "resume_data_revisions",
                        principalColumns: new[] { "id", "resume_version_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_writing_inputs_resume_versions_resume_version_id_candidate_~",
                        columns: x => new { x.resume_version_id, x.candidate_id },
                        principalTable: "resume_versions",
                        principalColumns: new[] { "id", "candidate_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "writing_results",
                columns: table => new
                {
                    ai_job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sections = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_writing_results", x => x.ai_job_id);
                    table.ForeignKey(
                        name: "FK_writing_results_writing_inputs_ai_job_id",
                        column: x => x.ai_job_id,
                        principalTable: "writing_inputs",
                        principalColumn: "ai_job_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "writing_decisions",
                columns: table => new
                {
                    ai_job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "text", nullable: false),
                    sections = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_writing_decisions", x => x.ai_job_id);
                    table.CheckConstraint("ck_writing_decision", "action IN ('accept','discard')");
                    table.ForeignKey(
                        name: "FK_writing_decisions_writing_results_ai_job_id",
                        column: x => x.ai_job_id,
                        principalTable: "writing_results",
                        principalColumn: "ai_job_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_job_type",
                table: "ai_jobs",
                sql: "job_type IN ('extract_cv','match_pair','suggest_cv') AND (job_type <> 'match_pair' OR resume_version_id IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_writing_inputs_resume_revision_id_resume_version_id",
                table: "writing_inputs",
                columns: new[] { "resume_revision_id", "resume_version_id" });

            migrationBuilder.CreateIndex(
                name: "IX_writing_inputs_resume_version_id_candidate_id",
                table: "writing_inputs",
                columns: new[] { "resume_version_id", "candidate_id" });
            migrationBuilder.Sql("""
                CREATE TRIGGER immutable_writing_input BEFORE UPDATE OR DELETE ON writing_inputs FOR EACH ROW EXECUTE FUNCTION guard_match_immutable();
                CREATE TRIGGER immutable_writing_result BEFORE UPDATE OR DELETE ON writing_results FOR EACH ROW EXECUTE FUNCTION guard_match_immutable();
                CREATE TRIGGER immutable_writing_decision BEFORE UPDATE OR DELETE ON writing_decisions FOR EACH ROW EXECUTE FUNCTION guard_match_immutable();
                CREATE FUNCTION guard_writing_context() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM ai_jobs j JOIN resume_data_revisions r ON r.id=NEW.resume_revision_id
                        WHERE j.id=NEW.ai_job_id AND j.job_type='suggest_cv' AND j.requested_by=NEW.candidate_id
                        AND j.resume_version_id=NEW.resume_version_id AND r.resume_version_id=NEW.resume_version_id
                        AND r.candidate_id=NEW.candidate_id AND r.confirmed_by=NEW.candidate_id AND r.confirmed_at IS NOT NULL
                        AND (r.origin='user_confirmed' OR r.origin='user_entered' AND r.schema_version=2))
                    THEN RAISE EXCEPTION 'invalid_writing_input' USING ERRCODE='23514'; END IF;
                    RETURN NEW;
                END; $$;
                CREATE TRIGGER writing_context BEFORE INSERT ON writing_inputs FOR EACH ROW EXECUTE FUNCTION guard_writing_context();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER writing_context ON writing_inputs;
                DROP FUNCTION guard_writing_context();
                DROP TRIGGER immutable_writing_decision ON writing_decisions;
                DROP TRIGGER immutable_writing_result ON writing_results;
                DROP TRIGGER immutable_writing_input ON writing_inputs;
                """);
            migrationBuilder.DropTable(
                name: "writing_decisions");

            migrationBuilder.DropTable(
                name: "writing_results");

            migrationBuilder.DropTable(
                name: "writing_inputs");

            migrationBuilder.DropCheckConstraint(
                name: "ck_job_type",
                table: "ai_jobs");

            migrationBuilder.AddCheckConstraint(
                name: "ck_job_type",
                table: "ai_jobs",
                sql: "job_type IN ('extract_cv','match_pair') AND (job_type <> 'match_pair' OR resume_version_id IS NULL)");
        }
    }
}
