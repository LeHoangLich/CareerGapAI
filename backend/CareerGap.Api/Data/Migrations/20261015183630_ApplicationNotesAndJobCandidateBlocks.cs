using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareerGap.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ApplicationNotesAndJobCandidateBlocks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "application_notes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_application_notes", x => x.id);
                    table.CheckConstraint("ck_application_note_body", "length(body) BETWEEN 1 AND 4000");
                    table.ForeignKey(
                        name: "FK_application_notes_AspNetUsers_author_user_id",
                        column: x => x.author_user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_application_notes_applications_application_id",
                        column: x => x.application_id,
                        principalTable: "applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "job_candidate_blocks",
                columns: table => new
                {
                    job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_candidate_blocks", x => new { x.job_id, x.candidate_id });
                    table.ForeignKey(
                        name: "FK_job_candidate_blocks_AspNetUsers_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_job_candidate_blocks_candidate_profiles_candidate_id",
                        column: x => x.candidate_id,
                        principalTable: "candidate_profiles",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_job_candidate_blocks_jobs_job_id",
                        column: x => x.job_id,
                        principalTable: "jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_application_notes_application_id_created_at_id",
                table: "application_notes",
                columns: new[] { "application_id", "created_at", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_application_notes_author_user_id",
                table: "application_notes",
                column: "author_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_job_candidate_blocks_candidate_id_created_at",
                table: "job_candidate_blocks",
                columns: new[] { "candidate_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_job_candidate_blocks_created_by_user_id",
                table: "job_candidate_blocks",
                column: "created_by_user_id");

            migrationBuilder.Sql("""
                CREATE FUNCTION prevent_application_note_mutation() RETURNS trigger LANGUAGE plpgsql AS $function$
                BEGIN
                    RAISE EXCEPTION 'application notes are append-only' USING ERRCODE = 'check_violation';
                END;
                $function$;
                CREATE TRIGGER trg_application_notes_immutable
                BEFORE UPDATE OR DELETE ON application_notes
                FOR EACH ROW EXECUTE FUNCTION prevent_application_note_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_application_notes_immutable ON application_notes;
                DROP FUNCTION IF EXISTS prevent_application_note_mutation();
                """);
            migrationBuilder.DropTable(
                name: "application_notes");

            migrationBuilder.DropTable(
                name: "job_candidate_blocks");
        }
    }
}
