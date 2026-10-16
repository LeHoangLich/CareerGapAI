using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareerGap.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RecruitmentJobsLocal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "companies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    slug = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    website_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    location = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    industry = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    size_label = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    row_version = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    archived_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_companies", x => x.id);
                    table.CheckConstraint("ck_company_row_version", "row_version >= 0");
                    table.ForeignKey(
                        name: "FK_companies_AspNetUsers_owner_user_id",
                        column: x => x.owner_user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "job_versions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    version_no = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    requirements_text = table.Column<string>(type: "text", nullable: false),
                    benefits_text = table.Column<string>(type: "text", nullable: true),
                    industry = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    location = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    employment_type = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    work_mode = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    experience_min = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    experience_max = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    salary_min = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    salary_max = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    salary_currency = table.Column<string>(type: "char(3)", nullable: true),
                    salary_period = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    language = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    content_hash = table.Column<string>(type: "char(64)", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_versions", x => x.id);
                    table.UniqueConstraint("AK_job_versions_id_job_id", x => new { x.id, x.job_id });
                    table.CheckConstraint("ck_job_version_fields", "version_no > 0 AND employment_type IN ('full_time','part_time','contract','internship','temporary') AND work_mode IN ('onsite','hybrid','remote','unspecified') AND language IN ('vi','en','vi-en') AND (experience_min IS NULL OR experience_min >= 0) AND (experience_max IS NULL OR experience_max >= 0) AND (experience_min IS NULL OR experience_max IS NULL OR experience_min <= experience_max) AND (salary_min IS NULL OR salary_min >= 0) AND (salary_max IS NULL OR salary_max >= 0) AND (salary_min IS NULL OR salary_max IS NULL OR salary_min <= salary_max) AND ((salary_min IS NULL AND salary_max IS NULL AND salary_currency IS NULL AND salary_period IS NULL) OR ((salary_min IS NOT NULL OR salary_max IS NOT NULL) AND salary_currency ~ '^[A-Z]{3}$' AND salary_period IN ('hour','day','month','year','project'))) AND length(content_hash) = 64");
                    table.ForeignKey(
                        name: "FK_job_versions_AspNetUsers_created_by",
                        column: x => x.created_by,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    current_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    application_deadline = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    row_version = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_jobs", x => x.id);
                    table.UniqueConstraint("AK_jobs_id_company_id", x => new { x.id, x.company_id });
                    table.CheckConstraint("ck_job_listing_state", "row_version >= 0 AND status IN ('draft','published','closed','archived') AND ((status = 'draft' AND closed_at IS NULL) OR (status = 'published' AND current_version_id IS NOT NULL AND published_at IS NOT NULL AND closed_at IS NULL) OR (status IN ('closed','archived') AND current_version_id IS NOT NULL AND published_at IS NOT NULL AND closed_at IS NOT NULL))");
                    table.ForeignKey(
                        name: "FK_jobs_companies_company_id",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_jobs_job_versions_current_version_id_id",
                        columns: x => new { x.current_version_id, x.id },
                        principalTable: "job_versions",
                        principalColumns: new[] { "id", "job_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "saved_jobs",
                columns: table => new
                {
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saved_jobs", x => new { x.candidate_id, x.job_id });
                    table.ForeignKey(
                        name: "FK_saved_jobs_candidate_profiles_candidate_id",
                        column: x => x.candidate_id,
                        principalTable: "candidate_profiles",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_saved_jobs_jobs_job_id",
                        column: x => x.job_id,
                        principalTable: "jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_companies_owner_user_id",
                table: "companies",
                column: "owner_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_companies_slug",
                table: "companies",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_job_versions_created_by",
                table: "job_versions",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_job_versions_job_id_version_no",
                table: "job_versions",
                columns: new[] { "job_id", "version_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_jobs_company_id_status_created_at",
                table: "jobs",
                columns: new[] { "company_id", "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_jobs_current_version_id_id",
                table: "jobs",
                columns: new[] { "current_version_id", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_jobs_status_created_at",
                table: "jobs",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_saved_jobs_job_id_created_at",
                table: "saved_jobs",
                columns: new[] { "job_id", "created_at" });

            migrationBuilder.AddForeignKey(
                name: "FK_job_versions_jobs_job_id",
                table: "job_versions",
                column: "job_id",
                principalTable: "jobs",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql("""
                CREATE FUNCTION prevent_job_version_mutation() RETURNS trigger LANGUAGE plpgsql AS $function$
                BEGIN
                    RAISE EXCEPTION 'job versions are immutable' USING ERRCODE = '23514';
                END;
                $function$;
                CREATE TRIGGER trg_job_versions_immutable BEFORE UPDATE OR DELETE ON job_versions
                    FOR EACH ROW EXECUTE FUNCTION prevent_job_version_mutation();

                CREATE FUNCTION prevent_company_owner_change() RETURNS trigger LANGUAGE plpgsql AS $function$
                BEGIN
                    IF NEW.owner_user_id IS DISTINCT FROM OLD.owner_user_id THEN
                        RAISE EXCEPTION 'company owner is immutable' USING ERRCODE = '23514';
                    END IF;
                    RETURN NEW;
                END;
                $function$;
                CREATE TRIGGER trg_company_owner_immutable BEFORE UPDATE OF owner_user_id ON companies
                    FOR EACH ROW EXECUTE FUNCTION prevent_company_owner_change();

                CREATE FUNCTION prevent_job_company_change() RETURNS trigger LANGUAGE plpgsql AS $function$
                BEGIN
                    IF NEW.company_id IS DISTINCT FROM OLD.company_id THEN
                        RAISE EXCEPTION 'job company is immutable' USING ERRCODE = '23514';
                    END IF;
                    RETURN NEW;
                END;
                $function$;
                CREATE TRIGGER trg_job_company_immutable BEFORE UPDATE OF company_id ON jobs
                    FOR EACH ROW EXECUTE FUNCTION prevent_job_company_change();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_job_company_immutable ON jobs;
                DROP TRIGGER IF EXISTS trg_company_owner_immutable ON companies;
                DROP TRIGGER IF EXISTS trg_job_versions_immutable ON job_versions;
                DROP FUNCTION IF EXISTS prevent_job_company_change();
                DROP FUNCTION IF EXISTS prevent_company_owner_change();
                DROP FUNCTION IF EXISTS prevent_job_version_mutation();
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_job_versions_jobs_job_id",
                table: "job_versions");

            migrationBuilder.DropTable(
                name: "saved_jobs");

            migrationBuilder.DropTable(
                name: "jobs");

            migrationBuilder.DropTable(
                name: "companies");

            migrationBuilder.DropTable(
                name: "job_versions");
        }
    }
}
