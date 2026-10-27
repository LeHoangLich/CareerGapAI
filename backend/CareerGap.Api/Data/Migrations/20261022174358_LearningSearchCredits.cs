using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareerGap.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class LearningSearchCredits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "learning_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pair_job_id = table.Column<Guid>(type: "uuid", nullable: true),
                    idempotency_key = table.Column<Guid>(type: "uuid", nullable: false),
                    input_hash = table.Column<string>(type: "text", nullable: false),
                    requirement_key = table.Column<string>(type: "text", nullable: false),
                    topic = table.Column<string>(type: "text", nullable: false),
                    language = table.Column<string>(type: "text", nullable: false),
                    level = table.Column<string>(type: "text", nullable: false),
                    provider = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    reserved_credits = table.Column<int>(type: "integer", nullable: false),
                    actual_credits = table.Column<int>(type: "integer", nullable: true),
                    period_start = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    period_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    links_json = table.Column<string>(type: "jsonb", nullable: true),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_learning_requests", x => x.id);
                    table.CheckConstraint("ck_learning_request", "status IN ('sent','succeeded','no_results','outcome_unknown','cached') AND reserved_credits >= 0 AND (actual_credits IS NULL OR actual_credits >= 0) AND period_start < period_end");
                    table.ForeignKey(
                        name: "FK_learning_requests_AspNetUsers_owner_id",
                        column: x => x.owner_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_learning_requests_ai_jobs_pair_job_id",
                        column: x => x.pair_job_id,
                        principalTable: "ai_jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_learning_requests_owner_id_idempotency_key",
                table: "learning_requests",
                columns: new[] { "owner_id", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_learning_requests_pair_job_id",
                table: "learning_requests",
                column: "pair_job_id");

            migrationBuilder.CreateIndex(
                name: "IX_learning_requests_provider_period_start",
                table: "learning_requests",
                columns: new[] { "provider", "period_start" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "learning_requests");
        }
    }
}
