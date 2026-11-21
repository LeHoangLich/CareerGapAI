using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareerGap.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class SavedWeeklyRoadmaps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "saved_roadmaps",
                columns: table => new
                {
                    PairId = table.Column<Guid>(type: "uuid", nullable: false),
                    Plan = table.Column<string>(type: "jsonb", nullable: false),
                    Completed = table.Column<string>(type: "jsonb", nullable: false),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saved_roadmaps", x => x.PairId);
                    table.ForeignKey(
                        name: "FK_saved_roadmaps_match_inputs_PairId",
                        column: x => x.PairId,
                        principalTable: "match_inputs",
                        principalColumn: "ai_job_id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "saved_roadmaps");
        }
    }
}
