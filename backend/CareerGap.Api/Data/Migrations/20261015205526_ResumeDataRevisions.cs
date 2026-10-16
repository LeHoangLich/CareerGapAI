using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareerGap.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ResumeDataRevisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "resume_data_revisions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    resume_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision_no = table.Column<int>(type: "integer", nullable: false),
                    parent_revision_id = table.Column<Guid>(type: "uuid", nullable: true),
                    origin = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    structured_data = table.Column<string>(type: "jsonb", nullable: false),
                    source_text = table.Column<string>(type: "jsonb", nullable: false),
                    evidence = table.Column<string>(type: "jsonb", nullable: false),
                    data_hash = table.Column<string>(type: "char(64)", nullable: false),
                    parser_version = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    method_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ai_job_id = table.Column<Guid>(type: "uuid", nullable: true),
                    confirmed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_resume_data_revisions", x => x.id);
                    table.UniqueConstraint("AK_resume_data_revisions_id_resume_version_id", x => new { x.id, x.resume_version_id });
                    table.CheckConstraint("ck_resume_data_revision", "revision_no > 0 AND schema_version > 0 AND length(data_hash) = 64 AND length(parser_version) BETWEEN 1 AND 80 AND origin IN ('ai_extracted','user_confirmed','user_entered') AND parent_revision_id IS DISTINCT FROM id AND ((origin = 'ai_extracted' AND method_version_id IS NOT NULL AND ai_job_id IS NOT NULL AND confirmed_by IS NULL AND confirmed_at IS NULL) OR (origin = 'user_confirmed' AND confirmed_by IS NOT NULL AND confirmed_at IS NOT NULL) OR (origin = 'user_entered' AND confirmed_by IS NOT NULL AND confirmed_at IS NOT NULL))");
                    table.ForeignKey(
                        name: "FK_resume_data_revisions_AspNetUsers_confirmed_by",
                        column: x => x.confirmed_by,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_resume_data_revisions_ai_jobs_ai_job_id",
                        column: x => x.ai_job_id,
                        principalTable: "ai_jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_resume_data_revisions_ai_method_versions_method_version_id",
                        column: x => x.method_version_id,
                        principalTable: "ai_method_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_resume_data_revisions_resume_data_revisions_parent_revision~",
                        columns: x => new { x.parent_revision_id, x.resume_version_id },
                        principalTable: "resume_data_revisions",
                        principalColumns: new[] { "id", "resume_version_id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_resume_data_revisions_resume_versions_resume_version_id_can~",
                        columns: x => new { x.resume_version_id, x.candidate_id },
                        principalTable: "resume_versions",
                        principalColumns: new[] { "id", "candidate_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql("""
                CREATE FUNCTION prevent_resume_data_revision_mutation() RETURNS trigger LANGUAGE plpgsql AS $function$
                BEGIN
                    IF TG_OP = 'DELETE' AND current_setting('careergap.account_purge', true) = 'on' THEN RETURN OLD; END IF;
                    RAISE EXCEPTION 'resume data revisions are immutable' USING ERRCODE = '23514';
                END;
                $function$;
                CREATE TRIGGER trg_resume_data_revisions_immutable BEFORE UPDATE OR DELETE ON resume_data_revisions
                    FOR EACH ROW EXECUTE FUNCTION prevent_resume_data_revision_mutation();
                """);

            migrationBuilder.CreateIndex(
                name: "IX_resume_data_revisions_ai_job_id",
                table: "resume_data_revisions",
                column: "ai_job_id",
                unique: true,
                filter: "ai_job_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_resume_data_revisions_candidate_id_resume_version_id_revisi~",
                table: "resume_data_revisions",
                columns: new[] { "candidate_id", "resume_version_id", "revision_no" });

            migrationBuilder.CreateIndex(
                name: "IX_resume_data_revisions_confirmed_by",
                table: "resume_data_revisions",
                column: "confirmed_by");

            migrationBuilder.CreateIndex(
                name: "IX_resume_data_revisions_method_version_id",
                table: "resume_data_revisions",
                column: "method_version_id");

            migrationBuilder.CreateIndex(
                name: "IX_resume_data_revisions_parent_revision_id_resume_version_id",
                table: "resume_data_revisions",
                columns: new[] { "parent_revision_id", "resume_version_id" });

            migrationBuilder.CreateIndex(
                name: "IX_resume_data_revisions_resume_version_id_candidate_id",
                table: "resume_data_revisions",
                columns: new[] { "resume_version_id", "candidate_id" });

            migrationBuilder.CreateIndex(
                name: "IX_resume_data_revisions_resume_version_id_revision_no",
                table: "resume_data_revisions",
                columns: new[] { "resume_version_id", "revision_no" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_resume_data_revisions_immutable ON resume_data_revisions;
                DROP FUNCTION IF EXISTS prevent_resume_data_revision_mutation();
                """);
            migrationBuilder.DropTable(
                name: "resume_data_revisions");
        }
    }
}
