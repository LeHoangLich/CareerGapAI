using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareerGap.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeSavedRoadmapsAndCommunitySearches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_saved_roadmaps_match_inputs_PairId",
                table: "saved_roadmaps");

            migrationBuilder.DropPrimaryKey(
                name: "PK_community_searchs",
                table: "community_searchs");

            migrationBuilder.RenameTable(
                name: "community_searchs",
                newName: "community_searches");

            migrationBuilder.RenameColumn(
                name: "Plan",
                table: "saved_roadmaps",
                newName: "plan");

            migrationBuilder.RenameColumn(
                name: "Completed",
                table: "saved_roadmaps",
                newName: "completed");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "saved_roadmaps",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "RowVersion",
                table: "saved_roadmaps",
                newName: "row_version");

            migrationBuilder.RenameColumn(
                name: "PairId",
                table: "saved_roadmaps",
                newName: "pair_id");

            migrationBuilder.RenameIndex(
                name: "IX_community_searchs_term_day",
                table: "community_searches",
                newName: "IX_community_searches_term_day");

            migrationBuilder.AddPrimaryKey(
                name: "PK_community_searches",
                table: "community_searches",
                columns: new[] { "term", "viewer", "day" });

            migrationBuilder.AddForeignKey(
                name: "FK_saved_roadmaps_match_inputs_pair_id",
                table: "saved_roadmaps",
                column: "pair_id",
                principalTable: "match_inputs",
                principalColumn: "ai_job_id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_saved_roadmaps_match_inputs_pair_id",
                table: "saved_roadmaps");

            migrationBuilder.DropPrimaryKey(
                name: "PK_community_searches",
                table: "community_searches");

            migrationBuilder.RenameTable(
                name: "community_searches",
                newName: "community_searchs");

            migrationBuilder.RenameColumn(
                name: "plan",
                table: "saved_roadmaps",
                newName: "Plan");

            migrationBuilder.RenameColumn(
                name: "completed",
                table: "saved_roadmaps",
                newName: "Completed");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "saved_roadmaps",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "row_version",
                table: "saved_roadmaps",
                newName: "RowVersion");

            migrationBuilder.RenameColumn(
                name: "pair_id",
                table: "saved_roadmaps",
                newName: "PairId");

            migrationBuilder.RenameIndex(
                name: "IX_community_searches_term_day",
                table: "community_searchs",
                newName: "IX_community_searchs_term_day");

            migrationBuilder.AddPrimaryKey(
                name: "PK_community_searchs",
                table: "community_searchs",
                columns: new[] { "term", "viewer", "day" });

            migrationBuilder.AddForeignKey(
                name: "FK_saved_roadmaps_match_inputs_PairId",
                table: "saved_roadmaps",
                column: "PairId",
                principalTable: "match_inputs",
                principalColumn: "ai_job_id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
