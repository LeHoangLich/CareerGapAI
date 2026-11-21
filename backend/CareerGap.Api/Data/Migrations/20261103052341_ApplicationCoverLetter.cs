using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareerGap.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ApplicationCoverLetter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "cover_letter",
                table: "applications",
                type: "character varying(5000)",
                maxLength: 5000,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_application_cover_letter",
                table: "applications",
                sql: "cover_letter IS NULL OR length(cover_letter) BETWEEN 1 AND 5000");

            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION prevent_application_snapshot_change() RETURNS trigger LANGUAGE plpgsql AS $function$
                BEGIN
                    IF NEW.id IS DISTINCT FROM OLD.id
                       OR NEW.candidate_id IS DISTINCT FROM OLD.candidate_id
                       OR NEW.job_id IS DISTINCT FROM OLD.job_id
                       OR NEW.submitted_resume_version_id IS DISTINCT FROM OLD.submitted_resume_version_id
                       OR NEW.submitted_job_version_id IS DISTINCT FROM OLD.submitted_job_version_id
                       OR NEW.submitted_at IS DISTINCT FROM OLD.submitted_at
                       OR NEW.submission_key IS DISTINCT FROM OLD.submission_key
                       OR NEW.submission_hash IS DISTINCT FROM OLD.submission_hash
                       OR NEW.cover_letter IS DISTINCT FROM OLD.cover_letter THEN
                        RAISE EXCEPTION 'application snapshot is immutable' USING ERRCODE = '23514';
                    END IF;
                    RETURN NEW;
                END;
                $function$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION prevent_application_snapshot_change() RETURNS trigger LANGUAGE plpgsql AS $function$
                BEGIN
                    IF NEW.id IS DISTINCT FROM OLD.id
                       OR NEW.candidate_id IS DISTINCT FROM OLD.candidate_id
                       OR NEW.job_id IS DISTINCT FROM OLD.job_id
                       OR NEW.submitted_resume_version_id IS DISTINCT FROM OLD.submitted_resume_version_id
                       OR NEW.submitted_job_version_id IS DISTINCT FROM OLD.submitted_job_version_id
                       OR NEW.submitted_at IS DISTINCT FROM OLD.submitted_at
                       OR NEW.submission_key IS DISTINCT FROM OLD.submission_key
                       OR NEW.submission_hash IS DISTINCT FROM OLD.submission_hash THEN
                        RAISE EXCEPTION 'application snapshot is immutable' USING ERRCODE = '23514';
                    END IF;
                    RETURN NEW;
                END;
                $function$;
                """);
            migrationBuilder.DropCheckConstraint(
                name: "ck_application_cover_letter",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "cover_letter",
                table: "applications");
        }
    }
}
