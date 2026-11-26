using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareerGap.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class StageP3DiscoveryFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "community_searchs",
                columns: table => new
                {
                    term = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    viewer = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    day = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_searchs", x => new { x.term, x.viewer, x.day });
                });

            migrationBuilder.CreateTable(
                name: "job_views",
                columns: table => new
                {
                    job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    viewer = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    day = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_views", x => new { x.job_id, x.viewer, x.day });
                    table.ForeignKey(
                        name: "FK_job_views_jobs_job_id",
                        column: x => x.job_id,
                        principalTable: "jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_community_searchs_term_day",
                table: "community_searchs",
                columns: new[] { "term", "day" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "community_searchs");

            migrationBuilder.DropTable(
                name: "job_views");
        }
    }
}
