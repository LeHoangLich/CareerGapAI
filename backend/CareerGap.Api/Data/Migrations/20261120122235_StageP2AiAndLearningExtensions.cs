using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareerGap.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class StageP2AiAndLearningExtensions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_job_type",
                table: "ai_jobs");

            migrationBuilder.AlterColumn<Guid>(
                name: "job_version_id",
                table: "match_inputs",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "job_revision_id",
                table: "match_inputs",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "career_context",
                table: "match_inputs",
                type: "character varying(24)",
                maxLength: 24,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "quick_match_jd_id",
                table: "match_inputs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "analysis_history_hides",
                columns: table => new
                {
                    ai_job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hidden_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_analysis_history_hides", x => x.ai_job_id);
                    table.ForeignKey(
                        name: "FK_analysis_history_hides_ai_jobs_ai_job_id",
                        column: x => x.ai_job_id,
                        principalTable: "ai_jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "job_rewrite_inputs",
                columns: table => new
                {
                    ai_job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recruiter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_revision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fingerprint = table.Column<string>(type: "char(64)", nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_rewrite_inputs", x => x.ai_job_id);
                    table.ForeignKey(
                        name: "FK_job_rewrite_inputs_ai_jobs_ai_job_id",
                        column: x => x.ai_job_id,
                        principalTable: "ai_jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_job_rewrite_inputs_job_data_revisions_job_revision_id_job_v~",
                        columns: x => new { x.job_revision_id, x.job_version_id },
                        principalTable: "job_data_revisions",
                        principalColumns: new[] { "id", "job_version_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_job_rewrite_inputs_job_versions_job_version_id_job_id",
                        columns: x => new { x.job_version_id, x.job_id },
                        principalTable: "job_versions",
                        principalColumns: new[] { "id", "job_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_job_rewrite_inputs_jobs_job_id",
                        column: x => x.job_id,
                        principalTable: "jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "quick_match_jds",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    source_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    raw_text = table.Column<string>(type: "text", nullable: false),
                    requirements_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quick_match_jds", x => x.id);
                    table.CheckConstraint("ck_quick_match_jd_source", "source_type IN ('paste','url')");
                    table.ForeignKey(
                        name: "FK_quick_match_jds_AspNetUsers_candidate_id",
                        column: x => x.candidate_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "job_rewrite_results",
                columns: table => new
                {
                    ai_job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sections = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_rewrite_results", x => x.ai_job_id);
                    table.ForeignKey(
                        name: "FK_job_rewrite_results_job_rewrite_inputs_ai_job_id",
                        column: x => x.ai_job_id,
                        principalTable: "job_rewrite_inputs",
                        principalColumn: "ai_job_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_match_inputs_quick_match_jd_id",
                table: "match_inputs",
                column: "quick_match_jd_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_match_career_context",
                table: "match_inputs",
                sql: "career_context IS NULL OR career_context IN ('career_start','career_switch','promotion')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_match_source",
                table: "match_inputs",
                sql: "((job_version_id IS NOT NULL AND job_revision_id IS NOT NULL AND quick_match_jd_id IS NULL) OR (job_version_id IS NULL AND job_revision_id IS NULL AND quick_match_jd_id IS NOT NULL AND context = 'self'))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_job_type",
                table: "ai_jobs",
                sql: "job_type IN ('extract_cv','match_pair','suggest_cv','rewrite_jd') AND (job_type NOT IN ('match_pair','rewrite_jd') OR resume_version_id IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_job_rewrite_inputs_job_id",
                table: "job_rewrite_inputs",
                column: "job_id");

            migrationBuilder.CreateIndex(
                name: "IX_job_rewrite_inputs_job_revision_id_job_version_id",
                table: "job_rewrite_inputs",
                columns: new[] { "job_revision_id", "job_version_id" });

            migrationBuilder.CreateIndex(
                name: "IX_job_rewrite_inputs_job_version_id_job_id",
                table: "job_rewrite_inputs",
                columns: new[] { "job_version_id", "job_id" });

            migrationBuilder.CreateIndex(
                name: "IX_quick_match_jds_candidate_id",
                table: "quick_match_jds",
                column: "candidate_id");

            migrationBuilder.AddForeignKey(
                name: "FK_match_inputs_quick_match_jds_quick_match_jd_id",
                table: "match_inputs",
                column: "quick_match_jd_id",
                principalTable: "quick_match_jds",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_match_inputs_quick_match_jds_quick_match_jd_id",
                table: "match_inputs");

            migrationBuilder.DropTable(
                name: "analysis_history_hides");

            migrationBuilder.DropTable(
                name: "job_rewrite_results");

            migrationBuilder.DropTable(
                name: "quick_match_jds");

            migrationBuilder.DropTable(
                name: "job_rewrite_inputs");

            migrationBuilder.DropIndex(
                name: "IX_match_inputs_quick_match_jd_id",
                table: "match_inputs");

            migrationBuilder.DropCheckConstraint(
                name: "ck_match_career_context",
                table: "match_inputs");

            migrationBuilder.DropCheckConstraint(
                name: "ck_match_source",
                table: "match_inputs");

            migrationBuilder.DropCheckConstraint(
                name: "ck_job_type",
                table: "ai_jobs");

            migrationBuilder.DropColumn(
                name: "career_context",
                table: "match_inputs");

            migrationBuilder.DropColumn(
                name: "quick_match_jd_id",
                table: "match_inputs");

            migrationBuilder.AlterColumn<Guid>(
                name: "job_version_id",
                table: "match_inputs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "job_revision_id",
                table: "match_inputs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_job_type",
                table: "ai_jobs",
                sql: "job_type IN ('extract_cv','match_pair','suggest_cv') AND (job_type <> 'match_pair' OR resume_version_id IS NULL)");
        }
    }
}
