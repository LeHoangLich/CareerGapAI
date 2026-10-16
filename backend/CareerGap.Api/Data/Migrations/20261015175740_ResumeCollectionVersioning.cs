using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareerGap.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ResumeCollectionVersioning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "row_version",
                table: "resumes",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "updated_at",
                table: "resumes",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddCheckConstraint(
                name: "ck_resume_row_version",
                table: "resumes",
                sql: "row_version >= 0");

            migrationBuilder.Sql("""
                CREATE FUNCTION prevent_resume_owner_change() RETURNS trigger LANGUAGE plpgsql AS $function$
                BEGIN
                    IF NEW.candidate_id IS DISTINCT FROM OLD.candidate_id THEN
                        RAISE EXCEPTION 'resume owner is immutable' USING ERRCODE = '23514';
                    END IF;
                    RETURN NEW;
                END;
                $function$;
                CREATE TRIGGER trg_resume_owner_immutable BEFORE UPDATE OF candidate_id ON resumes
                    FOR EACH ROW EXECUTE FUNCTION prevent_resume_owner_change();

                CREATE FUNCTION prevent_resume_version_mutation() RETURNS trigger LANGUAGE plpgsql AS $function$
                BEGIN
                    RAISE EXCEPTION 'resume versions are immutable' USING ERRCODE = '23514';
                END;
                $function$;
                CREATE TRIGGER trg_resume_versions_immutable BEFORE UPDATE OR DELETE ON resume_versions
                    FOR EACH ROW EXECUTE FUNCTION prevent_resume_version_mutation();

                CREATE FUNCTION require_verified_resume_file() RETURNS trigger LANGUAGE plpgsql AS $function$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM stored_files
                        WHERE id = NEW.file_id AND owner_user_id = NEW.candidate_id
                          AND status = 'ready' AND sha256 = NEW.document_hash AND verified_at IS NOT NULL
                    ) THEN
                        RAISE EXCEPTION 'resume version requires a verified owner file' USING ERRCODE = '23514';
                    END IF;
                    RETURN NEW;
                END;
                $function$;
                CREATE TRIGGER trg_resume_version_requires_verified_file BEFORE INSERT ON resume_versions
                    FOR EACH ROW EXECUTE FUNCTION require_verified_resume_file();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_resume_version_requires_verified_file ON resume_versions;
                DROP TRIGGER IF EXISTS trg_resume_versions_immutable ON resume_versions;
                DROP TRIGGER IF EXISTS trg_resume_owner_immutable ON resumes;
                DROP FUNCTION IF EXISTS require_verified_resume_file();
                DROP FUNCTION IF EXISTS prevent_resume_version_mutation();
                DROP FUNCTION IF EXISTS prevent_resume_owner_change();
                """);
            migrationBuilder.DropCheckConstraint(
                name: "ck_resume_row_version",
                table: "resumes");

            migrationBuilder.DropColumn(
                name: "row_version",
                table: "resumes");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "resumes");
        }
    }
}
