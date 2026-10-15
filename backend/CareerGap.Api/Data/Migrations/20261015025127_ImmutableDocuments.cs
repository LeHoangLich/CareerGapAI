using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareerGap.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ImmutableDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE FUNCTION guard_immutable_document() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN RAISE EXCEPTION 'immutable_document' USING ERRCODE = '23514'; END;
                $$;
                CREATE TRIGGER immutable_resume_version BEFORE UPDATE ON resume_versions
                    FOR EACH ROW EXECUTE FUNCTION guard_immutable_document();
                CREATE TRIGGER immutable_ai_method BEFORE UPDATE ON ai_method_versions
                    FOR EACH ROW EXECUTE FUNCTION guard_immutable_document();
                CREATE FUNCTION guard_ready_file() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF OLD.status = 'ready' AND
                       (NEW.object_key, NEW.provider, NEW.owner_user_id, NEW.sha256, NEW.size_bytes, NEW.mime_type)
                       IS DISTINCT FROM
                       (OLD.object_key, OLD.provider, OLD.owner_user_id, OLD.sha256, OLD.size_bytes, OLD.mime_type)
                    THEN RAISE EXCEPTION 'immutable_file_content' USING ERRCODE = '23514'; END IF;
                    RETURN NEW;
                END;
                $$;
                CREATE TRIGGER immutable_ready_file BEFORE UPDATE ON stored_files
                    FOR EACH ROW EXECUTE FUNCTION guard_ready_file();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER immutable_ready_file ON stored_files;
                DROP FUNCTION guard_ready_file();
                DROP TRIGGER immutable_ai_method ON ai_method_versions;
                DROP TRIGGER immutable_resume_version ON resume_versions;
                DROP FUNCTION guard_immutable_document();
                """);
        }
    }
}
