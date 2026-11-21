using Microsoft.EntityFrameworkCore;

namespace CareerGap.Api.Data;

public sealed class CommunityProfile
{
    public Guid UserId { get; set; }
    public string Handle { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Bio { get; set; } = "";
    public Guid? AvatarId { get; set; }
    public bool AllowMentions { get; set; } = true;
    public bool ShowCareer { get; set; }
    public long RowVersion { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class CommunityPost
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AuthorId { get; set; }
    public string Title { get; set; } = "";
    public string Slug { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Body { get; set; } = "";
    public string Category { get; set; } = "career";
    public Guid? ThumbnailId { get; set; }
    public string Status { get; set; } = "draft";
    public string? PreviousStatus { get; set; }
    public long RowVersion { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? PublishedAt { get; set; }
}
public sealed class CommunityTag { public string Name { get; set; } = ""; }
public sealed class CommunityPostTag { public Guid PostId { get; set; } public string Tag { get; set; } = ""; }
public sealed class CommunityComment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PostId { get; set; }
    public Guid AuthorId { get; set; }
    public Guid? ParentId { get; set; }
    public string Body { get; set; } = "";
    public bool IsDeleted { get; set; }
    public long RowVersion { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class CommunityReaction { public Guid PostId { get; set; } public Guid UserId { get; set; } public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow; }
public sealed class CommunityBookmark { public Guid PostId { get; set; } public Guid UserId { get; set; } public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow; }
public sealed class CommunityFollow { public Guid FollowerId { get; set; } public Guid FollowingId { get; set; } public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow; }
public sealed class CommunityView { public Guid PostId { get; set; } public string Viewer { get; set; } = ""; public DateOnly Day { get; set; } }
public sealed class CommunitySearch { public string Term { get; set; } = ""; public string Viewer { get; set; } = ""; public DateOnly Day { get; set; } }
public sealed class CommunityNotice
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RecipientId { get; set; }
    public Guid ActorId { get; set; }
    public Guid? PostId { get; set; }
    public string Kind { get; set; } = "";
    public string Message { get; set; } = "";
    public string DedupKey { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReadAt { get; set; }
}
public sealed class CommunityImage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public string ObjectKey { get; set; } = "";
    public string Provider { get; set; } = "";
    public string ContentType { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class CommunityReport
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReporterId { get; set; }
    public Guid PostId { get; set; }
    public Guid? CommentId { get; set; }
    public string Reason { get; set; } = "";
    public string Status { get; set; } = "open";
    public string Resolution { get; set; } = "";
    public Guid? ReviewedBy { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public long RowVersion { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
public sealed partial class AppDb
{
    public DbSet<CommunityProfile> CommunityProfiles => Set<CommunityProfile>();
    public DbSet<CommunityPost> CommunityPosts => Set<CommunityPost>();
    public DbSet<CommunityTag> CommunityTags => Set<CommunityTag>();
    public DbSet<CommunityPostTag> CommunityPostTags => Set<CommunityPostTag>();
    public DbSet<CommunityComment> CommunityComments => Set<CommunityComment>();
    public DbSet<CommunityReaction> CommunityReactions => Set<CommunityReaction>();
    public DbSet<CommunityBookmark> CommunityBookmarks => Set<CommunityBookmark>();
    public DbSet<CommunityFollow> CommunityFollows => Set<CommunityFollow>();
    public DbSet<CommunityView> CommunityViews => Set<CommunityView>();
    public DbSet<CommunitySearch> CommunitySearches => Set<CommunitySearch>();
    public DbSet<CommunityNotice> CommunityNotices => Set<CommunityNotice>();
    public DbSet<CommunityImage> CommunityImages => Set<CommunityImage>();
    public DbSet<CommunityReport> CommunityReports => Set<CommunityReport>();

    private static void ConfigureCommunity(ModelBuilder b)
    {
        b.Entity<CommunityProfile>().HasKey(x => x.UserId);
        b.Entity<CommunityProfile>().HasIndex(x => x.Handle).IsUnique();
        b.Entity<CommunityProfile>().Property(x => x.Handle).HasMaxLength(32);
        b.Entity<CommunityProfile>().Property(x => x.DisplayName).HasMaxLength(100);
        b.Entity<CommunityProfile>().Property(x => x.Bio).HasMaxLength(1000);
        b.Entity<CommunityProfile>().Property(x => x.RowVersion).IsConcurrencyToken();
        b.Entity<CommunityProfile>().HasOne<AppUser>().WithOne().HasForeignKey<CommunityProfile>(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<CommunityPost>().HasIndex(x => x.Slug).IsUnique();
        b.Entity<CommunityPost>().HasIndex(x => new { x.Status, x.PublishedAt, x.Id });
        b.Entity<CommunityPost>().HasIndex(x => new { x.AuthorId, x.CreatedAt });
        b.Entity<CommunityPost>().HasOne<AppUser>().WithMany().HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<CommunityPost>().Property(x => x.RowVersion).IsConcurrencyToken();
        b.Entity<CommunityPost>().Property(x => x.Title).HasMaxLength(200);
        b.Entity<CommunityPost>().Property(x => x.Slug).HasMaxLength(260);
        b.Entity<CommunityPost>().Property(x => x.Summary).HasMaxLength(500);
        b.Entity<CommunityPost>().Property(x => x.Body).HasMaxLength(30000);
        b.Entity<CommunityPost>().Property(x => x.Category).HasMaxLength(32);
        b.Entity<CommunityPost>().ToTable(t => t.HasCheckConstraint("ck_community_post_state", "status IN ('draft','pending','published','hidden','deleted') AND row_version >= 0"));
        b.Entity<CommunityTag>().HasKey(x => x.Name);
        b.Entity<CommunityTag>().Property(x => x.Name).HasMaxLength(40);
        b.Entity<CommunityPostTag>().HasKey(x => new { x.PostId, x.Tag });
        b.Entity<CommunityPostTag>().HasOne<CommunityPost>().WithMany().HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<CommunityPostTag>().HasOne<CommunityTag>().WithMany().HasForeignKey(x => x.Tag).OnDelete(DeleteBehavior.Restrict);
        b.Entity<CommunityComment>().HasOne<CommunityPost>().WithMany().HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<CommunityComment>().HasOne<AppUser>().WithMany().HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<CommunityComment>().HasOne<CommunityComment>().WithMany().HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<CommunityComment>().Property(x => x.Body).HasMaxLength(4000);
        b.Entity<CommunityComment>().Property(x => x.RowVersion).IsConcurrencyToken();
        b.Entity<CommunityComment>().HasIndex(x => new { x.PostId, x.CreatedAt, x.Id });
        b.Entity<CommunityReaction>().HasKey(x => new { x.PostId, x.UserId });
        b.Entity<CommunityReaction>().HasOne<CommunityPost>().WithMany().HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<CommunityBookmark>().HasKey(x => new { x.PostId, x.UserId });
        b.Entity<CommunityBookmark>().HasOne<CommunityPost>().WithMany().HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<CommunityFollow>().HasKey(x => new { x.FollowerId, x.FollowingId });
        b.Entity<CommunityFollow>().HasIndex(x => x.FollowingId);
        b.Entity<CommunityFollow>().ToTable(t => t.HasCheckConstraint("ck_community_follow", "follower_id <> following_id"));
        b.Entity<CommunityView>().HasKey(x => new { x.PostId, x.Viewer, x.Day });
        b.Entity<CommunityView>().Property(x => x.Viewer).HasMaxLength(64);
        b.Entity<CommunityView>().HasOne<CommunityPost>().WithMany().HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<CommunitySearch>().ToTable("community_searches");
        b.Entity<CommunitySearch>().HasKey(x => new { x.Term, x.Viewer, x.Day });
        b.Entity<CommunitySearch>().Property(x => x.Term).HasMaxLength(40);
        b.Entity<CommunitySearch>().Property(x => x.Viewer).HasMaxLength(64);
        b.Entity<CommunitySearch>().HasIndex(x => new { x.Term, x.Day });
        b.Entity<CommunityNotice>().HasIndex(x => x.DedupKey).IsUnique();
        b.Entity<CommunityNotice>().Property(x => x.DedupKey).HasMaxLength(250);
        b.Entity<CommunityNotice>().Property(x => x.Message).HasMaxLength(600);
        b.Entity<CommunityNotice>().HasIndex(x => new { x.RecipientId, x.CreatedAt, x.Id });
        b.Entity<CommunityImage>().HasOne<AppUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<CommunityImage>().Property(x => x.ObjectKey).HasMaxLength(64);
        b.Entity<CommunityImage>().Property(x => x.ContentType).HasMaxLength(40);
        b.Entity<CommunityReport>().HasIndex(x => new { x.Status, x.CreatedAt });
        b.Entity<CommunityReport>().Property(x => x.Reason).HasMaxLength(1000);
        b.Entity<CommunityReport>().Property(x => x.Resolution).HasMaxLength(1000);
        b.Entity<CommunityReport>().Property(x => x.RowVersion).IsConcurrencyToken();
        b.Entity<CommunityReport>().ToTable(t => t.HasCheckConstraint("ck_community_report_state", "status IN ('open','reviewed','resolved','dismissed') AND row_version >= 0"));
    }
}
