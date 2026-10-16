using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareerGap.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class OrphanFileCleanupLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_file_ready",
                table: "stored_files");

            migrationBuilder.AddColumn<int>(
                name: "cleanup_attempt_count",
                table: "stored_files",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "cleanup_last_error_code",
                table: "stored_files",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "cleanup_lease_until",
                table: "stored_files",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "cleanup_next_attempt_at",
                table: "stored_files",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_stored_files_cleanup",
                table: "stored_files",
                columns: new[] { "status", "created_at", "cleanup_next_attempt_at", "cleanup_lease_until" },
                filter: "status IN ('pending','delete_pending')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_file_ready",
                table: "stored_files",
                sql: "size_bytes > 0 AND status IN ('pending','ready','failed','delete_pending') AND cleanup_attempt_count >= 0 AND (status <> 'ready' OR (sha256 IS NOT NULL AND verified_at IS NOT NULL))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $guard$
                BEGIN
                    IF EXISTS (SELECT 1 FROM stored_files WHERE status = 'delete_pending' OR cleanup_attempt_count > 0
                        OR cleanup_next_attempt_at IS NOT NULL OR cleanup_lease_until IS NOT NULL OR cleanup_last_error_code IS NOT NULL) THEN
                        RAISE EXCEPTION 'cannot remove pending file cleanup state';
                    END IF;
                END;
                $guard$;
                """);
            migrationBuilder.DropIndex(
                name: "ix_stored_files_cleanup",
                table: "stored_files");

            migrationBuilder.DropCheckConstraint(
                name: "ck_file_ready",
                table: "stored_files");

            migrationBuilder.DropColumn(
                name: "cleanup_attempt_count",
                table: "stored_files");

            migrationBuilder.DropColumn(
                name: "cleanup_last_error_code",
                table: "stored_files");

            migrationBuilder.DropColumn(
                name: "cleanup_lease_until",
                table: "stored_files");

            migrationBuilder.DropColumn(
                name: "cleanup_next_attempt_at",
                table: "stored_files");

            migrationBuilder.AddCheckConstraint(
                name: "ck_file_ready",
                table: "stored_files",
                sql: "size_bytes > 0 AND status IN ('pending','ready','failed') AND (status <> 'ready' OR (sha256 IS NOT NULL AND verified_at IS NOT NULL))");
        }
    }
}
