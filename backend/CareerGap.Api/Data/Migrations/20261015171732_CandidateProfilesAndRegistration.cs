using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareerGap.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class CandidateProfilesAndRegistration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "contact_email",
                table: "candidate_profiles",
                type: "character varying(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "contact_phone",
                table: "candidate_profiles",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "desired_role",
                table: "candidate_profiles",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "details",
                table: "candidate_profiles",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'{}'::jsonb");

            migrationBuilder.AddColumn<int>(
                name: "details_schema_version",
                table: "candidate_profiles",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<decimal>(
                name: "experience_years",
                table: "candidate_profiles",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "headline",
                table: "candidate_profiles",
                type: "character varying(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "location",
                table: "candidate_profiles",
                type: "character varying(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "preferred_work_mode",
                table: "candidate_profiles",
                type: "character varying(24)",
                maxLength: 24,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "row_version",
                table: "candidate_profiles",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "summary",
                table: "candidate_profiles",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "candidate_profile_visibility",
                columns: table => new
                {
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    field_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candidate_profile_visibility", x => new { x.candidate_id, x.field_key });
                    table.ForeignKey(
                        name: "FK_candidate_profile_visibility_candidate_profiles_candidate_id",
                        column: x => x.candidate_id,
                        principalTable: "candidate_profiles",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_candidate_profile",
                table: "candidate_profiles",
                sql: "(experience_years IS NULL OR experience_years >= 0) AND details_schema_version > 0 AND row_version >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "candidate_profile_visibility");

            migrationBuilder.DropCheckConstraint(
                name: "ck_candidate_profile",
                table: "candidate_profiles");

            migrationBuilder.DropColumn(
                name: "contact_email",
                table: "candidate_profiles");

            migrationBuilder.DropColumn(
                name: "contact_phone",
                table: "candidate_profiles");

            migrationBuilder.DropColumn(
                name: "desired_role",
                table: "candidate_profiles");

            migrationBuilder.DropColumn(
                name: "details",
                table: "candidate_profiles");

            migrationBuilder.DropColumn(
                name: "details_schema_version",
                table: "candidate_profiles");

            migrationBuilder.DropColumn(
                name: "experience_years",
                table: "candidate_profiles");

            migrationBuilder.DropColumn(
                name: "headline",
                table: "candidate_profiles");

            migrationBuilder.DropColumn(
                name: "location",
                table: "candidate_profiles");

            migrationBuilder.DropColumn(
                name: "preferred_work_mode",
                table: "candidate_profiles");

            migrationBuilder.DropColumn(
                name: "row_version",
                table: "candidate_profiles");

            migrationBuilder.DropColumn(
                name: "summary",
                table: "candidate_profiles");
        }
    }
}
