using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareerGap.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AccountDeletionLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION prevent_application_history_mutation() RETURNS trigger LANGUAGE plpgsql AS $function$
                BEGIN
                    IF TG_OP = 'DELETE' AND current_setting('careergap.account_purge', true) = 'on' THEN RETURN OLD; END IF;
                    RAISE EXCEPTION 'application history is append-only' USING ERRCODE = '23514';
                END;
                $function$;
                CREATE OR REPLACE FUNCTION prevent_application_note_mutation() RETURNS trigger LANGUAGE plpgsql AS $function$
                BEGIN
                    IF TG_OP = 'DELETE' AND current_setting('careergap.account_purge', true) = 'on' THEN RETURN OLD; END IF;
                    RAISE EXCEPTION 'application notes are append-only' USING ERRCODE = '23514';
                END;
                $function$;
                CREATE OR REPLACE FUNCTION prevent_resume_version_mutation() RETURNS trigger LANGUAGE plpgsql AS $function$
                BEGIN
                    IF TG_OP = 'DELETE' AND current_setting('careergap.account_purge', true) = 'on' THEN RETURN OLD; END IF;
                    RAISE EXCEPTION 'resume versions are immutable' USING ERRCODE = '23514';
                END;
                $function$;
                CREATE FUNCTION allow_application_purge_delete() RETURNS trigger LANGUAGE plpgsql AS $function$
                BEGIN
                    IF current_setting('careergap.account_purge', true) = 'on' THEN RETURN OLD; END IF;
                    RAISE EXCEPTION 'application snapshot deletion requires account purge' USING ERRCODE = '23514';
                END;
                $function$;
                CREATE TRIGGER trg_application_purge_delete BEFORE DELETE ON applications
                    FOR EACH ROW EXECUTE FUNCTION allow_application_purge_delete();
                CREATE OR REPLACE FUNCTION guard_job_input() RETURNS trigger LANGUAGE plpgsql AS $function$
                BEGIN
                    IF current_setting('careergap.account_purge', true) = 'on' THEN
                        IF (NEW.id, NEW.requested_by, NEW.method_version_id, NEW.idempotency_key, NEW.max_attempts, NEW.created_at,
                            NEW.status, NEW.attempt_count, NEW.lease_generation, NEW.lease_owner, NEW.lease_until, NEW.available_at,
                            NEW.finished_at, NEW.error_code)
                           IS DISTINCT FROM
                           (OLD.id, OLD.requested_by, OLD.method_version_id, OLD.idempotency_key, OLD.max_attempts, OLD.created_at,
                            OLD.status, OLD.attempt_count, OLD.lease_generation, OLD.lease_owner, OLD.lease_until, OLD.available_at,
                            OLD.finished_at, OLD.error_code)
                           OR (NEW.resume_version_id IS NOT NULL AND NEW.resume_version_id IS DISTINCT FROM OLD.resume_version_id)
                           OR (NEW.input_hash <> '' AND NEW.input_hash IS DISTINCT FROM OLD.input_hash)
                           OR (NEW.output_json IS NOT NULL AND NEW.output_json IS DISTINCT FROM OLD.output_json)
                        THEN RAISE EXCEPTION 'invalid account purge job erasure' USING ERRCODE = '23514'; END IF;
                        RETURN NEW;
                    END IF;
                    IF (NEW.requested_by, NEW.resume_version_id, NEW.method_version_id, NEW.idempotency_key, NEW.input_hash, NEW.max_attempts, NEW.created_at)
                       IS DISTINCT FROM
                       (OLD.requested_by, OLD.resume_version_id, OLD.method_version_id, OLD.idempotency_key, OLD.input_hash, OLD.max_attempts, OLD.created_at)
                    THEN RAISE EXCEPTION 'immutable_job_input' USING ERRCODE = '23514'; END IF;
                    IF OLD.status = 'succeeded' AND (NEW.output_json, NEW.status) IS DISTINCT FROM (OLD.output_json, OLD.status)
                    THEN RAISE EXCEPTION 'immutable_job_result' USING ERRCODE = '23514'; END IF;
                    RETURN NEW;
                END;
                $function$;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "resume_version_id",
                table: "ai_jobs",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.CreateTable(
                name: "account_deletion_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    purge_after = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    data_purged_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    purged_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    lease_owner = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    lease_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    lease_generation = table.Column<long>(type: "bigint", nullable: false),
                    last_error_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_deletion_requests", x => x.id);
                    table.CheckConstraint("ck_account_deletion_request_state", "status IN ('pending','cancelled','purged') AND purge_after > requested_at AND attempt_count >= 0 AND lease_generation >= 0 AND ((status = 'cancelled' AND cancelled_at IS NOT NULL AND data_purged_at IS NULL AND purged_at IS NULL) OR (status = 'pending' AND cancelled_at IS NULL AND purged_at IS NULL) OR (status = 'purged' AND cancelled_at IS NULL AND data_purged_at IS NOT NULL AND purged_at IS NOT NULL))");
                    table.ForeignKey(
                        name: "FK_account_deletion_requests_AspNetUsers_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "account_deletion_objects",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    object_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_error_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_deletion_objects", x => x.id);
                    table.CheckConstraint("ck_account_deletion_object_state", "status IN ('pending','deleted') AND attempt_count >= 0 AND ((status = 'deleted' AND deleted_at IS NOT NULL) OR (status = 'pending' AND deleted_at IS NULL))");
                    table.ForeignKey(
                        name: "FK_account_deletion_objects_account_deletion_requests_request_~",
                        column: x => x.request_id,
                        principalTable: "account_deletion_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_account_deletion_objects_request_id_provider_object_key",
                table: "account_deletion_objects",
                columns: new[] { "request_id", "provider", "object_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_account_deletion_objects_request_id_status_created_at",
                table: "account_deletion_objects",
                columns: new[] { "request_id", "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_account_deletion_due",
                table: "account_deletion_requests",
                columns: new[] { "purge_after", "requested_at", "id" },
                filter: "status = 'pending'");

            migrationBuilder.CreateIndex(
                name: "IX_account_deletion_requests_user_id_requested_at",
                table: "account_deletion_requests",
                columns: new[] { "user_id", "requested_at" });

            migrationBuilder.CreateIndex(
                name: "ux_account_deletion_one_pending",
                table: "account_deletion_requests",
                column: "user_id",
                unique: true,
                filter: "status = 'pending'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $function$
                BEGIN
                    IF EXISTS (SELECT 1 FROM account_deletion_requests) OR EXISTS (SELECT 1 FROM ai_jobs WHERE resume_version_id IS NULL) THEN
                        RAISE EXCEPTION 'cannot roll back account deletion lifecycle after deletion data was recorded';
                    END IF;
                END;
                $function$;
                DROP TRIGGER IF EXISTS trg_application_purge_delete ON applications;
                DROP FUNCTION IF EXISTS allow_application_purge_delete();
                CREATE OR REPLACE FUNCTION prevent_application_history_mutation() RETURNS trigger LANGUAGE plpgsql AS $function$
                BEGIN
                    RAISE EXCEPTION 'application history is append-only' USING ERRCODE = '23514';
                END;
                $function$;
                CREATE OR REPLACE FUNCTION prevent_application_note_mutation() RETURNS trigger LANGUAGE plpgsql AS $function$
                BEGIN
                    RAISE EXCEPTION 'application notes are append-only' USING ERRCODE = 'check_violation';
                END;
                $function$;
                CREATE OR REPLACE FUNCTION prevent_resume_version_mutation() RETURNS trigger LANGUAGE plpgsql AS $function$
                BEGIN
                    RAISE EXCEPTION 'resume versions are immutable' USING ERRCODE = '23514';
                END;
                $function$;
                CREATE OR REPLACE FUNCTION guard_job_input() RETURNS trigger LANGUAGE plpgsql AS $function$
                BEGIN
                    IF (NEW.requested_by, NEW.resume_version_id, NEW.method_version_id, NEW.idempotency_key, NEW.input_hash, NEW.max_attempts, NEW.created_at)
                       IS DISTINCT FROM
                       (OLD.requested_by, OLD.resume_version_id, OLD.method_version_id, OLD.idempotency_key, OLD.input_hash, OLD.max_attempts, OLD.created_at)
                    THEN RAISE EXCEPTION 'immutable_job_input' USING ERRCODE = '23514'; END IF;
                    IF OLD.status = 'succeeded' AND (NEW.output_json, NEW.status) IS DISTINCT FROM (OLD.output_json, OLD.status)
                    THEN RAISE EXCEPTION 'immutable_job_result' USING ERRCODE = '23514'; END IF;
                    RETURN NEW;
                END;
                $function$;
                """);
            migrationBuilder.DropTable(
                name: "account_deletion_objects");

            migrationBuilder.DropTable(
                name: "account_deletion_requests");

            migrationBuilder.AlterColumn<Guid>(
                name: "resume_version_id",
                table: "ai_jobs",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
