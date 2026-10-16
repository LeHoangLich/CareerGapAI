using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareerGap.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ApplicationSnapshotsAndNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "applications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submitted_resume_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submitted_job_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status_changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    withdrawn_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    submission_key = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_hash = table.Column<string>(type: "char(64)", nullable: false),
                    row_version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_applications", x => x.id);
                    table.UniqueConstraint("AK_applications_id_candidate_id", x => new { x.id, x.candidate_id });
                    table.UniqueConstraint("AK_applications_id_job_id", x => new { x.id, x.job_id });
                    table.CheckConstraint("ck_application_state", "status IN ('submitted','reviewing','shortlisted','rejected','withdrawn') AND row_version >= 0 AND length(submission_hash) = 64 AND ((status = 'withdrawn' AND withdrawn_at IS NOT NULL) OR (status <> 'withdrawn' AND withdrawn_at IS NULL))");
                    table.ForeignKey(
                        name: "FK_applications_candidate_profiles_candidate_id",
                        column: x => x.candidate_id,
                        principalTable: "candidate_profiles",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_applications_job_versions_submitted_job_version_id_job_id",
                        columns: x => new { x.submitted_job_version_id, x.job_id },
                        principalTable: "job_versions",
                        principalColumns: new[] { "id", "job_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_applications_jobs_job_id",
                        column: x => x.job_id,
                        principalTable: "jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_applications_resume_versions_submitted_resume_version_id_ca~",
                        columns: x => new { x.submitted_resume_version_id, x.candidate_id },
                        principalTable: "resume_versions",
                        principalColumns: new[] { "id", "candidate_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "application_status_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    to_status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reason_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_application_status_history", x => x.id);
                    table.CheckConstraint("ck_application_history_status", "(from_status IS NULL OR from_status IN ('submitted','reviewing','shortlisted','rejected','withdrawn')) AND to_status IN ('submitted','reviewing','shortlisted','rejected','withdrawn')");
                    table.ForeignKey(
                        name: "FK_application_status_history_AspNetUsers_actor_user_id",
                        column: x => x.actor_user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_application_status_history_applications_application_id",
                        column: x => x.application_id,
                        principalTable: "applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    notification_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ai_job_id = table.Column<Guid>(type: "uuid", nullable: true),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    body = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    read_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deduplication_key = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifications", x => x.id);
                    table.CheckConstraint("ck_notification_reference", "application_id IS NULL OR ai_job_id IS NULL");
                    table.ForeignKey(
                        name: "FK_notifications_AspNetUsers_recipient_user_id",
                        column: x => x.recipient_user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_notifications_ai_jobs_ai_job_id",
                        column: x => x.ai_job_id,
                        principalTable: "ai_jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_notifications_applications_application_id",
                        column: x => x.application_id,
                        principalTable: "applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_application_status_history_actor_user_id",
                table: "application_status_history",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_application_status_history_application_id_changed_at_id",
                table: "application_status_history",
                columns: new[] { "application_id", "changed_at", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_applications_candidate_id_job_id",
                table: "applications",
                columns: new[] { "candidate_id", "job_id" },
                unique: true,
                filter: "status IN ('submitted','reviewing','shortlisted')");

            migrationBuilder.CreateIndex(
                name: "IX_applications_candidate_id_submission_key",
                table: "applications",
                columns: new[] { "candidate_id", "submission_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_applications_job_id_status_submitted_at",
                table: "applications",
                columns: new[] { "job_id", "status", "submitted_at" });

            migrationBuilder.CreateIndex(
                name: "IX_applications_submitted_job_version_id_job_id",
                table: "applications",
                columns: new[] { "submitted_job_version_id", "job_id" });

            migrationBuilder.CreateIndex(
                name: "IX_applications_submitted_resume_version_id_candidate_id",
                table: "applications",
                columns: new[] { "submitted_resume_version_id", "candidate_id" });

            migrationBuilder.CreateIndex(
                name: "IX_notifications_ai_job_id",
                table: "notifications",
                column: "ai_job_id");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_application_id",
                table: "notifications",
                column: "application_id");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_recipient_created",
                table: "notifications",
                columns: new[] { "recipient_user_id", "created_at", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_notifications_recipient_user_id_deduplication_key",
                table: "notifications",
                columns: new[] { "recipient_user_id", "deduplication_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notifications_unread",
                table: "notifications",
                columns: new[] { "recipient_user_id", "created_at", "id" },
                filter: "read_at IS NULL");

            migrationBuilder.Sql("""
                CREATE FUNCTION prevent_application_snapshot_change() RETURNS trigger LANGUAGE plpgsql AS $function$
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
                CREATE TRIGGER trg_application_snapshot_immutable BEFORE UPDATE ON applications
                    FOR EACH ROW EXECUTE FUNCTION prevent_application_snapshot_change();

                CREATE FUNCTION prevent_application_history_mutation() RETURNS trigger LANGUAGE plpgsql AS $function$
                BEGIN
                    RAISE EXCEPTION 'application history is append-only' USING ERRCODE = '23514';
                END;
                $function$;
                CREATE TRIGGER trg_application_history_immutable BEFORE UPDATE OR DELETE ON application_status_history
                    FOR EACH ROW EXECUTE FUNCTION prevent_application_history_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_application_history_immutable ON application_status_history;
                DROP TRIGGER IF EXISTS trg_application_snapshot_immutable ON applications;
                DROP FUNCTION IF EXISTS prevent_application_history_mutation();
                DROP FUNCTION IF EXISTS prevent_application_snapshot_change();
                """);
            migrationBuilder.DropTable(
                name: "application_status_history");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "applications");
        }
    }
}
