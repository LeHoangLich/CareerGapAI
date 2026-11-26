using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareerGap.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReportReviewWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_content_report",
                table: "content_reports");

            migrationBuilder.AddCheckConstraint(
                name: "ck_content_report",
                table: "content_reports",
                sql: "status IN ('open','reviewed','resolved','dismissed') AND target_type IN ('job','public_resume') AND row_version >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Never silently erase an in-progress moderation state during downgrade.
            migrationBuilder.Sql("DO $$ BEGIN IF EXISTS (SELECT 1 FROM content_reports WHERE status='reviewed') THEN RAISE EXCEPTION 'Resolve reviewed reports before downgrading ReportReviewWorkflow'; END IF; END $$;");
            migrationBuilder.DropCheckConstraint(
                name: "ck_content_report",
                table: "content_reports");

            migrationBuilder.AddCheckConstraint(
                name: "ck_content_report",
                table: "content_reports",
                sql: "status IN ('open','resolved','dismissed') AND target_type IN ('job','public_resume') AND row_version >= 0");
        }
    }
}
