using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareerGap.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class JobCapacityAndSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_job_listing_state",
                table: "jobs");

            migrationBuilder.AddColumn<string>(
                name: "archived_by_role",
                table: "jobs",
                type: "character varying(24)",
                maxLength: 24,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "max_applications",
                table: "jobs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "previous_status",
                table: "jobs",
                type: "character varying(24)",
                maxLength: 24,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_job_listing_state",
                table: "jobs",
                sql: "row_version >= 0 AND (max_applications IS NULL OR max_applications BETWEEN 1 AND 10000) AND (archived_by_role IS NULL OR archived_by_role IN ('recruiter','admin')) AND (previous_status IS NULL OR previous_status IN ('draft','published','closed')) AND status IN ('draft','published','closed','archived') AND ((status = 'draft' AND closed_at IS NULL) OR (status = 'published' AND current_version_id IS NOT NULL AND published_at IS NOT NULL AND closed_at IS NULL) OR (status = 'closed' AND current_version_id IS NOT NULL AND published_at IS NOT NULL AND closed_at IS NOT NULL) OR (status = 'archived' AND current_version_id IS NOT NULL AND closed_at IS NOT NULL))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_job_listing_state",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "archived_by_role",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "max_applications",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "previous_status",
                table: "jobs");

            migrationBuilder.AddCheckConstraint(
                name: "ck_job_listing_state",
                table: "jobs",
                sql: "row_version >= 0 AND status IN ('draft','published','closed','archived') AND ((status = 'draft' AND closed_at IS NULL) OR (status = 'published' AND current_version_id IS NOT NULL AND published_at IS NOT NULL AND closed_at IS NULL) OR (status IN ('closed','archived') AND current_version_id IS NOT NULL AND published_at IS NOT NULL AND closed_at IS NOT NULL))");
        }
    }
}
