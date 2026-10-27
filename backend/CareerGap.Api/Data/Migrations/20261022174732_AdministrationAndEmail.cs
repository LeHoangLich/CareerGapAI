using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareerGap.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AdministrationAndEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "admin_audits",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: true),
                    action = table.Column<string>(type: "text", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_audits", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "content_reports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reporter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_type = table.Column<string>(type: "text", nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    row_version = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_content_reports", x => x.id);
                    table.CheckConstraint("ck_content_report", "status IN ('open','resolved','dismissed') AND target_type IN ('job','public_resume') AND row_version >= 0");
                    table.ForeignKey(
                        name: "FK_content_reports_AspNetUsers_reporter_id",
                        column: x => x.reporter_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "email_deliverys",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    period_start = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    period_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_deliverys", x => x.id);
                    table.CheckConstraint("ck_email_delivery", "status IN ('sent','accepted','outcome_unknown') AND period_start < period_end");
                });

            migrationBuilder.CreateIndex(
                name: "IX_admin_audits_created_at",
                table: "admin_audits",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_content_reports_reporter_id",
                table: "content_reports",
                column: "reporter_id");

            migrationBuilder.CreateIndex(
                name: "IX_content_reports_status_created_at",
                table: "content_reports",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_email_deliverys_provider_period_start",
                table: "email_deliverys",
                columns: new[] { "provider", "period_start" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "admin_audits");

            migrationBuilder.DropTable(
                name: "content_reports");

            migrationBuilder.DropTable(
                name: "email_deliverys");
        }
    }
}
