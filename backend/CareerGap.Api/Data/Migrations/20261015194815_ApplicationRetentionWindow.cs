using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareerGap.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ApplicationRetentionWindow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION prevent_application_history_mutation() RETURNS trigger LANGUAGE plpgsql AS $function$
                BEGIN
                    IF TG_OP = 'DELETE' AND (current_setting('careergap.account_purge', true) = 'on'
                       OR current_setting('careergap.application_retention', true) = 'on') THEN RETURN OLD; END IF;
                    RAISE EXCEPTION 'application history is append-only' USING ERRCODE = '23514';
                END;
                $function$;
                CREATE OR REPLACE FUNCTION prevent_application_note_mutation() RETURNS trigger LANGUAGE plpgsql AS $function$
                BEGIN
                    IF TG_OP = 'DELETE' AND (current_setting('careergap.account_purge', true) = 'on'
                       OR current_setting('careergap.application_retention', true) = 'on') THEN RETURN OLD; END IF;
                    RAISE EXCEPTION 'application notes are append-only' USING ERRCODE = '23514';
                END;
                $function$;
                CREATE OR REPLACE FUNCTION allow_application_purge_delete() RETURNS trigger LANGUAGE plpgsql AS $function$
                BEGIN
                    IF current_setting('careergap.account_purge', true) = 'on'
                       OR current_setting('careergap.application_retention', true) = 'on' THEN RETURN OLD; END IF;
                    RAISE EXCEPTION 'application snapshot deletion requires retention or account purge' USING ERRCODE = '23514';
                END;
                $function$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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
                CREATE OR REPLACE FUNCTION allow_application_purge_delete() RETURNS trigger LANGUAGE plpgsql AS $function$
                BEGIN
                    IF current_setting('careergap.account_purge', true) = 'on' THEN RETURN OLD; END IF;
                    RAISE EXCEPTION 'application snapshot deletion requires account purge' USING ERRCODE = '23514';
                END;
                $function$;
                """);
        }
    }
}
