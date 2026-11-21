using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareerGap.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class CommunityNetwork : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "community_follows",
                columns: table => new
                {
                    follower_id = table.Column<Guid>(type: "uuid", nullable: false),
                    following_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_follows", x => new { x.follower_id, x.following_id });
                    table.CheckConstraint("ck_community_follow", "follower_id <> following_id");
                });

            migrationBuilder.CreateTable(
                name: "community_images",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    object_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    provider = table.Column<string>(type: "text", nullable: false),
                    content_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_images", x => x.id);
                    table.ForeignKey(
                        name: "FK_community_images_AspNetUsers_owner_id",
                        column: x => x.owner_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "community_notices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    post_id = table.Column<Guid>(type: "uuid", nullable: true),
                    kind = table.Column<string>(type: "text", nullable: false),
                    message = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    dedup_key = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    read_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_notices", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "community_posts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    slug = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    body = table.Column<string>(type: "character varying(30000)", maxLength: 30000, nullable: false),
                    category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    thumbnail_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    previous_status = table.Column<string>(type: "text", nullable: true),
                    row_version = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_posts", x => x.id);
                    table.CheckConstraint("ck_community_post_state", "status IN ('draft','pending','published','hidden','deleted') AND row_version >= 0");
                    table.ForeignKey(
                        name: "FK_community_posts_AspNetUsers_author_id",
                        column: x => x.author_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "community_profiles",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    handle = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    display_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    bio = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    avatar_id = table.Column<Guid>(type: "uuid", nullable: true),
                    allow_mentions = table.Column<bool>(type: "boolean", nullable: false),
                    row_version = table.Column<long>(type: "bigint", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_profiles", x => x.user_id);
                    table.ForeignKey(
                        name: "FK_community_profiles_AspNetUsers_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "community_reports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reporter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    post_id = table.Column<Guid>(type: "uuid", nullable: false),
                    comment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    resolution = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    row_version = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_reports", x => x.id);
                    table.CheckConstraint("ck_community_report_state", "status IN ('open','reviewed','resolved','dismissed') AND row_version >= 0");
                });

            migrationBuilder.CreateTable(
                name: "community_tags",
                columns: table => new
                {
                    name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_tags", x => x.name);
                });

            migrationBuilder.CreateTable(
                name: "community_bookmarks",
                columns: table => new
                {
                    post_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_bookmarks", x => new { x.post_id, x.user_id });
                    table.ForeignKey(
                        name: "FK_community_bookmarks_community_posts_post_id",
                        column: x => x.post_id,
                        principalTable: "community_posts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "community_comments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    post_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    row_version = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_comments", x => x.id);
                    table.ForeignKey(
                        name: "FK_community_comments_AspNetUsers_author_id",
                        column: x => x.author_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_community_comments_community_comments_parent_id",
                        column: x => x.parent_id,
                        principalTable: "community_comments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_community_comments_community_posts_post_id",
                        column: x => x.post_id,
                        principalTable: "community_posts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "community_reactions",
                columns: table => new
                {
                    post_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_reactions", x => new { x.post_id, x.user_id });
                    table.ForeignKey(
                        name: "FK_community_reactions_community_posts_post_id",
                        column: x => x.post_id,
                        principalTable: "community_posts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "community_views",
                columns: table => new
                {
                    post_id = table.Column<Guid>(type: "uuid", nullable: false),
                    viewer = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    day = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_views", x => new { x.post_id, x.viewer, x.day });
                    table.ForeignKey(
                        name: "FK_community_views_community_posts_post_id",
                        column: x => x.post_id,
                        principalTable: "community_posts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "community_post_tags",
                columns: table => new
                {
                    post_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tag = table.Column<string>(type: "character varying(40)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_post_tags", x => new { x.post_id, x.tag });
                    table.ForeignKey(
                        name: "FK_community_post_tags_community_posts_post_id",
                        column: x => x.post_id,
                        principalTable: "community_posts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_community_post_tags_community_tags_tag",
                        column: x => x.tag,
                        principalTable: "community_tags",
                        principalColumn: "name",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_community_comments_author_id",
                table: "community_comments",
                column: "author_id");

            migrationBuilder.CreateIndex(
                name: "IX_community_comments_parent_id",
                table: "community_comments",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "IX_community_comments_post_id_created_at_id",
                table: "community_comments",
                columns: new[] { "post_id", "created_at", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_community_follows_following_id",
                table: "community_follows",
                column: "following_id");

            migrationBuilder.CreateIndex(
                name: "IX_community_images_owner_id",
                table: "community_images",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "IX_community_notices_dedup_key",
                table: "community_notices",
                column: "dedup_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_community_notices_recipient_id_created_at_id",
                table: "community_notices",
                columns: new[] { "recipient_id", "created_at", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_community_post_tags_tag",
                table: "community_post_tags",
                column: "tag");

            migrationBuilder.CreateIndex(
                name: "IX_community_posts_author_id_created_at",
                table: "community_posts",
                columns: new[] { "author_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_community_posts_slug",
                table: "community_posts",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_community_posts_status_published_at_id",
                table: "community_posts",
                columns: new[] { "status", "published_at", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_community_profiles_handle",
                table: "community_profiles",
                column: "handle",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_community_reports_status_created_at",
                table: "community_reports",
                columns: new[] { "status", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "community_bookmarks");

            migrationBuilder.DropTable(
                name: "community_comments");

            migrationBuilder.DropTable(
                name: "community_follows");

            migrationBuilder.DropTable(
                name: "community_images");

            migrationBuilder.DropTable(
                name: "community_notices");

            migrationBuilder.DropTable(
                name: "community_post_tags");

            migrationBuilder.DropTable(
                name: "community_profiles");

            migrationBuilder.DropTable(
                name: "community_reactions");

            migrationBuilder.DropTable(
                name: "community_reports");

            migrationBuilder.DropTable(
                name: "community_views");

            migrationBuilder.DropTable(
                name: "community_tags");

            migrationBuilder.DropTable(
                name: "community_posts");
        }
    }
}
