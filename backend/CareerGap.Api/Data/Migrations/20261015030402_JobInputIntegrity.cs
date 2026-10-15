using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareerGap.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class JobInputIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE FUNCTION guard_job_input() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF (NEW.requested_by, NEW.resume_version_id, NEW.method_version_id, NEW.idempotency_key, NEW.input_hash, NEW.max_attempts, NEW.created_at)
                       IS DISTINCT FROM
                       (OLD.requested_by, OLD.resume_version_id, OLD.method_version_id, OLD.idempotency_key, OLD.input_hash, OLD.max_attempts, OLD.created_at)
                    THEN RAISE EXCEPTION 'immutable_job_input' USING ERRCODE = '23514'; END IF;
                    IF OLD.status = 'succeeded' AND (NEW.output_json, NEW.status) IS DISTINCT FROM (OLD.output_json, OLD.status)
                    THEN RAISE EXCEPTION 'immutable_job_result' USING ERRCODE = '23514'; END IF;
                    RETURN NEW;
                END;
                $$;
                CREATE TRIGGER immutable_job_input BEFORE UPDATE ON ai_jobs FOR EACH ROW EXECUTE FUNCTION guard_job_input();
                CREATE FUNCTION guard_resume_owner() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF NEW.candidate_id IS DISTINCT FROM OLD.candidate_id
                    THEN RAISE EXCEPTION 'immutable_resume_owner' USING ERRCODE = '23514'; END IF;
                    RETURN NEW;
                END;
                $$;
                CREATE TRIGGER immutable_resume_owner BEFORE UPDATE ON resumes FOR EACH ROW EXECUTE FUNCTION guard_resume_owner();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER immutable_resume_owner ON resumes;
                DROP FUNCTION guard_resume_owner();
                DROP TRIGGER immutable_job_input ON ai_jobs;
                DROP FUNCTION guard_job_input();
                """);
        }
    }
}
