namespace CareerGap.Api.Community;

public sealed record ProfileEdit(string? Handle, string? DisplayName, string? Bio, Guid? AvatarId, bool AllowMentions, long RowVersion, bool? ShowCareer = null);
public sealed record PostEdit(string? Title, string? Summary, string? Body, string? Category, string[]? Tags, Guid? ThumbnailId, bool Submit, long RowVersion);
public sealed record VersionRequest(long RowVersion);
public sealed record CommentEdit(string? Body, Guid? ParentId, long RowVersion);
public sealed record ReportCreate(Guid PostId, Guid? CommentId, string? Reason);
public sealed record ModerationAction(string? Action, string? Reason, long RowVersion, long? PostRowVersion = null, long? CommentRowVersion = null);
public sealed record SocialProfile(Guid UserId, string Handle, string DisplayName, string Bio, Guid? AvatarId, bool AllowMentions, long RowVersion, int Followers, int Following, int Posts, bool IsFollowing, bool ShowCareer = false, SocialCareer? Career = null);
public sealed record SocialPost(Guid Id, Guid AuthorId, string Author, string Handle, Guid? AvatarId, string Title, string Slug, string Summary,
    string? Body, string Category, string[] Tags, Guid? ThumbnailId, string Status, long RowVersion, DateTimeOffset CreatedAt,
    DateTimeOffset? PublishedAt, int Votes, int Comments, int Bookmarks, int Views, bool Voted, bool Bookmarked);
public sealed record SocialComment(Guid Id, Guid PostId, Guid AuthorId, string Author, string Handle, Guid? AvatarId, Guid? ParentId,
    string Body, bool IsDeleted, long RowVersion, DateTimeOffset CreatedAt);
public sealed record SocialPage<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);
public sealed class CommunityError(int status, string code) : Exception(code)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}

public sealed record SocialCareer(string Role, string? Headline, string? DesiredRole, string? Location,
    decimal? ExperienceYears, string[] Skills, SocialCompany? Company);
public sealed record SocialCompany(Guid Id, string Name, string? Description, string? Industry,
    string? SizeLabel, string? Location, string? WebsiteUrl, string? LogoUrl);
