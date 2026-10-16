using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareerGap.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class JobDataRevisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "job_data_revisions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision_no = table.Column<int>(type: "integer", nullable: false),
                    parent_revision_id = table.Column<Guid>(type: "uuid", nullable: true),
                    origin = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    structured_data = table.Column<string>(type: "jsonb", nullable: false),
                    source_text = table.Column<string>(type: "jsonb", nullable: false),
                    evidence = table.Column<string>(type: "jsonb", nullable: false),
                    data_hash = table.Column<string>(type: "char(64)", nullable: false),
                    parser_version = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    confirmed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_data_revisions", x => x.id);
                    table.UniqueConstraint("AK_job_data_revisions_id_job_version_id", x => new { x.id, x.job_version_id });
                    table.CheckConstraint("ck_job_data_revision", "revision_no > 0 AND schema_version > 0 AND length(data_hash) = 64 AND length(parser_version) BETWEEN 1 AND 80 AND origin IN ('local_extracted','user_confirmed') AND parent_revision_id IS DISTINCT FROM id AND ((origin = 'local_extracted' AND confirmed_by IS NULL AND confirmed_at IS NULL) OR (origin = 'user_confirmed' AND confirmed_by IS NOT NULL AND confirmed_at IS NOT NULL))");
                    table.ForeignKey(
                        name: "FK_job_data_revisions_AspNetUsers_confirmed_by",
                        column: x => x.confirmed_by,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_job_data_revisions_job_data_revisions_parent_revision_id_jo~",
                        columns: x => new { x.parent_revision_id, x.job_version_id },
                        principalTable: "job_data_revisions",
                        principalColumns: new[] { "id", "job_version_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_job_data_revisions_job_versions_job_version_id",
                        column: x => x.job_version_id,
                        principalTable: "job_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_job_data_revisions_confirmed_by",
                table: "job_data_revisions",
                column: "confirmed_by");

            migrationBuilder.CreateIndex(
                name: "IX_job_data_revisions_job_version_id_created_at",
                table: "job_data_revisions",
                columns: new[] { "job_version_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_job_data_revisions_job_version_id_revision_no",
                table: "job_data_revisions",
                columns: new[] { "job_version_id", "revision_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_job_data_revisions_parent_revision_id_job_version_id",
                table: "job_data_revisions",
                columns: new[] { "parent_revision_id", "job_version_id" });

            migrationBuilder.Sql("""
                CREATE FUNCTION reject_job_data_revision_mutation() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'job_data_revision_immutable' USING ERRCODE = '23514';
                END $$;
                CREATE TRIGGER trg_job_data_revisions_immutable
                    BEFORE UPDATE OR DELETE ON job_data_revisions
                    FOR EACH ROW EXECUTE FUNCTION reject_job_data_revision_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_job_data_revisions_immutable ON job_data_revisions;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS reject_job_data_revision_mutation();");
            migrationBuilder.DropTable(
                name: "job_data_revisions");
        }
    }
}
