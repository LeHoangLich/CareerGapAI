using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareerGap.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ProfileTimestampsAndVisibilityConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "created_at",
                table: "candidate_profiles",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "updated_at",
                table: "candidate_profiles",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddCheckConstraint(
                name: "ck_profile_visibility_field",
                table: "candidate_profile_visibility",
                sql: "field_key IN ('display_name','headline','summary','location','contact_email','contact_phone','desired_role','preferred_work_mode','experience_years','skills','experience','education','projects','certifications')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_profile_visibility_field",
                table: "candidate_profile_visibility");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "candidate_profiles");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "candidate_profiles");
        }
    }
}
