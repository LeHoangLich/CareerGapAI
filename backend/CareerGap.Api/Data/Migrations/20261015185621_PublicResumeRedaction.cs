using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareerGap.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class PublicResumeRedaction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "candidate_public_resumes",
                columns: table => new
                {
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    resume_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    redacted_file_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_hash = table.Column<string>(type: "char(64)", nullable: false),
                    redaction_method_version = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    redaction_verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    row_version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candidate_public_resumes", x => x.candidate_id);
                    table.CheckConstraint("ck_candidate_public_resume", "length(source_hash) = 64 AND row_version >= 0 AND status IN ('not_ready','ready') AND ((status = 'ready' AND redacted_file_id IS NOT NULL AND redaction_method_version IS NOT NULL AND redaction_verified_at IS NOT NULL) OR (status = 'not_ready' AND redacted_file_id IS NULL AND redaction_method_version IS NULL AND redaction_verified_at IS NULL))");
                    table.ForeignKey(
                        name: "FK_candidate_public_resumes_candidate_profiles_candidate_id",
                        column: x => x.candidate_id,
                        principalTable: "candidate_profiles",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_candidate_public_resumes_resume_versions_resume_version_id_~",
                        columns: x => new { x.resume_version_id, x.candidate_id },
                        principalTable: "resume_versions",
                        principalColumns: new[] { "id", "candidate_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_candidate_public_resumes_stored_files_redacted_file_id_cand~",
                        columns: x => new { x.redacted_file_id, x.candidate_id },
                        principalTable: "stored_files",
                        principalColumns: new[] { "id", "owner_user_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_candidate_public_resumes_redacted_file_id_candidate_id",
                table: "candidate_public_resumes",
                columns: new[] { "redacted_file_id", "candidate_id" });

            migrationBuilder.CreateIndex(
                name: "IX_candidate_public_resumes_resume_version_id_candidate_id",
                table: "candidate_public_resumes",
                columns: new[] { "resume_version_id", "candidate_id" });

            migrationBuilder.CreateIndex(
                name: "IX_candidate_public_resumes_status_published_at_candidate_id",
                table: "candidate_public_resumes",
                columns: new[] { "status", "published_at", "candidate_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "candidate_public_resumes");
        }
    }
}
