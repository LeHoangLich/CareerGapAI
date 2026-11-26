using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareerGap.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class CompanyLogos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "logo_image_id",
                table: "companies",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_companies_logo_image_id",
                table: "companies",
                column: "logo_image_id");

            migrationBuilder.AddForeignKey(
                name: "FK_companies_community_images_logo_image_id",
                table: "companies",
                column: "logo_image_id",
                principalTable: "community_images",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_companies_community_images_logo_image_id",
                table: "companies");

            migrationBuilder.DropIndex(
                name: "IX_companies_logo_image_id",
                table: "companies");

            migrationBuilder.DropColumn(
                name: "logo_image_id",
                table: "companies");
        }
    }
}
