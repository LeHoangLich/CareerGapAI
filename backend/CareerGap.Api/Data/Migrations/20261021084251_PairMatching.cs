using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareerGap.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class PairMatching : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "job_type",
                table: "ai_jobs",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "extract_cv");

            migrationBuilder.CreateTable(
                name: "match_inputs",
                columns: table => new
                {
                    ai_job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    resume_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    resume_revision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_revision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: true),
                    context = table.Column<string>(type: "text", nullable: false),
                    fingerprint = table.Column<string>(type: "char(64)", nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_match_inputs", x => x.ai_job_id);
                    table.CheckConstraint("ck_match_context", "context IN ('self','public_pool','application') AND ((context = 'application') = (application_id IS NOT NULL))");
                    table.ForeignKey(
                        name: "FK_match_inputs_ai_jobs_ai_job_id",
                        column: x => x.ai_job_id,
                        principalTable: "ai_jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_match_inputs_applications_application_id",
                        column: x => x.application_id,
                        principalTable: "applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_match_inputs_job_data_revisions_job_revision_id_job_version~",
                        columns: x => new { x.job_revision_id, x.job_version_id },
                        principalTable: "job_data_revisions",
                        principalColumns: new[] { "id", "job_version_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_match_inputs_resume_data_revisions_resume_revision_id_resum~",
                        columns: x => new { x.resume_revision_id, x.resume_version_id },
                        principalTable: "resume_data_revisions",
                        principalColumns: new[] { "id", "resume_version_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_match_inputs_resume_versions_resume_version_id_candidate_id",
                        columns: x => new { x.resume_version_id, x.candidate_id },
                        principalTable: "resume_versions",
                        principalColumns: new[] { "id", "candidate_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "match_results",
                columns: table => new
                {
                    ai_job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    score = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    criteria = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_match_results", x => x.ai_job_id);
                    table.CheckConstraint("ck_match_score", "score BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "FK_match_results_match_inputs_ai_job_id",
                        column: x => x.ai_job_id,
                        principalTable: "match_inputs",
                        principalColumn: "ai_job_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_job_type",
                table: "ai_jobs",
                sql: "job_type IN ('extract_cv','match_pair') AND (job_type <> 'match_pair' OR resume_version_id IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_match_inputs_application_id",
                table: "match_inputs",
                column: "application_id");

            migrationBuilder.CreateIndex(
                name: "IX_match_inputs_fingerprint_ai_job_id",
                table: "match_inputs",
                columns: new[] { "fingerprint", "ai_job_id" });

            migrationBuilder.CreateIndex(
                name: "IX_match_inputs_job_revision_id_job_version_id",
                table: "match_inputs",
                columns: new[] { "job_revision_id", "job_version_id" });

            migrationBuilder.CreateIndex(
                name: "IX_match_inputs_resume_revision_id_resume_version_id",
                table: "match_inputs",
                columns: new[] { "resume_revision_id", "resume_version_id" });

            migrationBuilder.CreateIndex(
                name: "IX_match_inputs_resume_version_id_candidate_id",
                table: "match_inputs",
                columns: new[] { "resume_version_id", "candidate_id" });
            migrationBuilder.Sql("""
                CREATE FUNCTION guard_match_immutable() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'DELETE' AND (current_setting('careergap.account_purge', true) = 'on' OR
                        current_setting('careergap.application_retention', true) = 'on') THEN RETURN OLD; END IF;
                    RAISE EXCEPTION 'immutable_match_data' USING ERRCODE = '23514';
                END; $$;
                CREATE TRIGGER immutable_match_input BEFORE UPDATE OR DELETE ON match_inputs FOR EACH ROW EXECUTE FUNCTION guard_match_immutable();
                CREATE TRIGGER immutable_match_result BEFORE UPDATE OR DELETE ON match_results FOR EACH ROW EXECUTE FUNCTION guard_match_immutable();
                CREATE FUNCTION guard_match_context() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE requester uuid;
                BEGIN
                    SELECT requested_by INTO requester FROM ai_jobs WHERE id = NEW.ai_job_id AND job_type = 'match_pair';
                    IF requester IS NULL OR NOT EXISTS (SELECT 1 FROM resume_data_revisions r WHERE r.id = NEW.resume_revision_id
                        AND r.candidate_id = NEW.candidate_id AND r.confirmed_by = NEW.candidate_id AND r.confirmed_at IS NOT NULL)
                    THEN RAISE EXCEPTION 'invalid_match_input' USING ERRCODE = '23514'; END IF;
                    IF NEW.context = 'self' AND requester <> NEW.candidate_id
                    THEN RAISE EXCEPTION 'invalid_match_owner' USING ERRCODE = '23514'; END IF;
                    IF NEW.context <> 'self' AND NOT EXISTS (SELECT 1 FROM job_versions v JOIN jobs j ON j.id = v.job_id
                        JOIN companies c ON c.id = j.company_id WHERE v.id = NEW.job_version_id AND c.owner_user_id = requester)
                    THEN RAISE EXCEPTION 'invalid_match_owner' USING ERRCODE = '23514'; END IF;
                    IF NEW.context = 'application' AND NOT EXISTS (SELECT 1 FROM applications a JOIN job_versions v ON v.job_id = a.job_id
                        JOIN resume_data_revisions r ON r.id = NEW.resume_revision_id JOIN job_data_revisions d ON d.id = NEW.job_revision_id
                        WHERE a.id = NEW.application_id AND a.candidate_id = NEW.candidate_id
                        AND a.submitted_resume_version_id = NEW.resume_version_id AND a.submitted_job_version_id = NEW.job_version_id
                        AND v.id = NEW.job_version_id AND r.created_at <= a.submitted_at AND r.confirmed_at <= a.submitted_at
                        AND d.created_at <= a.submitted_at AND d.confirmed_at <= a.submitted_at)
                    THEN RAISE EXCEPTION 'invalid_application_match_snapshot' USING ERRCODE = '23514'; END IF;
                    RETURN NEW;
                END; $$;
                CREATE TRIGGER match_context BEFORE INSERT ON match_inputs FOR EACH ROW EXECUTE FUNCTION guard_match_context();
                CREATE FUNCTION guard_job_type() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF NEW.job_type IS DISTINCT FROM OLD.job_type THEN RAISE EXCEPTION 'immutable_job_type' USING ERRCODE = '23514'; END IF;
                    RETURN NEW;
                END; $$;
                CREATE TRIGGER immutable_job_type BEFORE UPDATE ON ai_jobs FOR EACH ROW EXECUTE FUNCTION guard_job_type();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER immutable_job_type ON ai_jobs;
                DROP FUNCTION guard_job_type();
                DROP TRIGGER match_context ON match_inputs;
                DROP FUNCTION guard_match_context();
                DROP TRIGGER immutable_match_input ON match_inputs;
                DROP TRIGGER immutable_match_result ON match_results;
                DROP FUNCTION guard_match_immutable();
                """);
            migrationBuilder.DropTable(
                name: "match_results");

            migrationBuilder.DropTable(
                name: "match_inputs");

            migrationBuilder.DropCheckConstraint(
                name: "ck_job_type",
                table: "ai_jobs");

            migrationBuilder.DropColumn(
                name: "job_type",
                table: "ai_jobs");
        }
    }
}
