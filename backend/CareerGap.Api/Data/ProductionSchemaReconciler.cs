using System.Security.Cryptography;
using System.Text.Json;
using CareerGap.Api.Recruitment;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CareerGap.Api.Data;

public static class ProductionSchemaReconciler
{
    public static async Task PrepareDatabaseAsync(string connectionString, AppDbSchemaOptions schemaOptions,
        IServiceProvider services, ILogger logger, bool seedShowcaseIfEmpty, CancellationToken ct = default)
    {
        await TryRawDdlReconciliationAsync(connectionString, logger, ct);
        schemaOptions.HasExtendedUserColumns = await ProbeExtendedUserColumnsAsync(connectionString, logger, ct);

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();

        if (schemaOptions.HasExtendedUserColumns)
        {
            await SafeStepAsync("ef_migrate_pending", async () =>
            {
                var pending = (await db.Database.GetPendingMigrationsAsync(ct)).ToArray();
                if (pending.Length > 0)
                {
                    logger.LogInformation("Applying {Count} pending EF Core migration(s).", pending.Length);
                    await db.Database.MigrateAsync(ct);
                }
            }, logger);
        }
        else
        {
            logger.LogWarning("AspNetUsers extended columns are not yet migrated (restricted runtime role); operating in compatible DML fallback mode.");
        }

        if (seedShowcaseIfEmpty)
            await SafeStepAsync("seed_showcase_if_empty", () => SeedShowcaseIfEmptyCoreAsync(db, logger, ct), logger);
    }

    public static async Task ReconcileAndSeedAsync(AppDb db, ILogger logger, bool seedShowcaseIfEmpty, CancellationToken ct = default)
    {
        var connStr = db.Database.GetConnectionString() ?? throw new InvalidOperationException("Missing connection string");
        await TryRawDdlReconciliationAsync(connStr, logger, ct);
        await SafeStepAsync("ef_migrate_pending", async () =>
        {
            var pending = (await db.Database.GetPendingMigrationsAsync(ct)).ToArray();
            if (pending.Length > 0) await db.Database.MigrateAsync(ct);
        }, logger);
        if (seedShowcaseIfEmpty)
            await SafeStepAsync("seed_showcase_if_empty", () => SeedShowcaseIfEmptyCoreAsync(db, logger, ct), logger);
    }

    public static async Task<bool> ProbeExtendedUserColumnsAsync(string connectionString, ILogger logger, CancellationToken ct = default)
    {
        try
        {
            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand("""
                SELECT count(*) FROM information_schema.columns
                WHERE table_schema = 'public' AND table_name = 'AspNetUsers'
                  AND column_name IN ('DisplayName', 'AvatarImageId', 'NotifyCommunity', 'NotifyRecruitment')
                """, conn);
            var count = Convert.ToInt64(await cmd.ExecuteScalarAsync(ct));
            return count == 4;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to probe AspNetUsers extended columns.");
            return true;
        }
    }

    private static async Task TryRawDdlReconciliationAsync(string connectionString, ILogger logger, CancellationToken ct)
    {
        await SafeStepAsync("reconcile_community_searches", async () =>
        {
            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand("""
                DO $$
                BEGIN
                  CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                    "MigrationId" character varying(150) NOT NULL PRIMARY KEY,
                    "ProductVersion" character varying(32) NOT NULL
                  );
                  IF to_regclass('public.community_searchs') IS NOT NULL THEN
                    IF to_regclass('public.community_searches') IS NULL THEN
                      ALTER TABLE community_searchs DROP CONSTRAINT IF EXISTS "PK_community_searchs";
                      ALTER TABLE community_searchs RENAME TO community_searches;
                    ELSE
                      EXECUTE 'INSERT INTO community_searches (term, viewer, day) SELECT term, viewer, day FROM community_searchs ON CONFLICT DO NOTHING';
                      EXECUTE 'DROP TABLE community_searchs';
                    END IF;
                  END IF;
                  IF to_regclass('public.community_searches') IS NOT NULL THEN
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid = to_regclass('public.community_searches') AND contype = 'p') THEN
                      ALTER TABLE community_searches ADD CONSTRAINT "PK_community_searches" PRIMARY KEY (term, viewer, day);
                    END IF;
                    IF to_regclass('public."IX_community_searchs_term_day"') IS NOT NULL AND to_regclass('public."IX_community_searches_term_day"') IS NULL THEN
                      ALTER INDEX "IX_community_searchs_term_day" RENAME TO "IX_community_searches_term_day";
                    END IF;
                    CREATE INDEX IF NOT EXISTS "IX_community_searches_term_day" ON community_searches (term, day);
                  END IF;
                END $$;
                """, conn);
            await cmd.ExecuteNonQueryAsync(ct);
        }, logger);

        await SafeStepAsync("reconcile_saved_roadmaps", async () =>
        {
            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand("""
                DO $$
                BEGIN
                  IF to_regclass('public.saved_roadmaps') IS NOT NULL THEN
                    ALTER TABLE saved_roadmaps DROP CONSTRAINT IF EXISTS "FK_saved_roadmaps_match_inputs_PairId";
                    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='saved_roadmaps' AND column_name='PairId')
                       AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='saved_roadmaps' AND column_name='pair_id') THEN
                      ALTER TABLE saved_roadmaps RENAME COLUMN "PairId" TO pair_id;
                    END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='saved_roadmaps' AND column_name='Plan')
                       AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='saved_roadmaps' AND column_name='plan') THEN
                      ALTER TABLE saved_roadmaps RENAME COLUMN "Plan" TO plan;
                    END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='saved_roadmaps' AND column_name='Completed')
                       AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='saved_roadmaps' AND column_name='completed') THEN
                      ALTER TABLE saved_roadmaps RENAME COLUMN "Completed" TO completed;
                    END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='saved_roadmaps' AND column_name='UpdatedAt')
                       AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='saved_roadmaps' AND column_name='updated_at') THEN
                      ALTER TABLE saved_roadmaps RENAME COLUMN "UpdatedAt" TO updated_at;
                    END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='saved_roadmaps' AND column_name='RowVersion')
                       AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='saved_roadmaps' AND column_name='row_version') THEN
                      ALTER TABLE saved_roadmaps RENAME COLUMN "RowVersion" TO row_version;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_saved_roadmaps_match_inputs_pair_id') THEN
                      ALTER TABLE saved_roadmaps ADD CONSTRAINT "FK_saved_roadmaps_match_inputs_pair_id"
                        FOREIGN KEY (pair_id) REFERENCES match_inputs (ai_job_id) ON DELETE CASCADE;
                    END IF;
                  END IF;
                END $$;
                """, conn);
            await cmd.ExecuteNonQueryAsync(ct);
        }, logger);

        await SafeStepAsync("reconcile_account_identity_columns", async () =>
        {
            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand("""
                ALTER TABLE "AspNetUsers" ADD COLUMN IF NOT EXISTS "AvatarImageId" uuid;
                ALTER TABLE "AspNetUsers" ADD COLUMN IF NOT EXISTS "DisplayName" character varying(150);
                ALTER TABLE "AspNetUsers" ADD COLUMN IF NOT EXISTS "NotifyCommunity" boolean NOT NULL DEFAULT TRUE;
                ALTER TABLE "AspNetUsers" ADD COLUMN IF NOT EXISTS "NotifyRecruitment" boolean NOT NULL DEFAULT TRUE;
                CREATE INDEX IF NOT EXISTS "IX_AspNetUsers_AvatarImageId" ON "AspNetUsers" ("AvatarImageId");
                DO $$
                BEGIN
                  IF to_regclass('public.community_images') IS NOT NULL AND NOT EXISTS (
                    SELECT 1 FROM pg_constraint WHERE conname = 'FK_AspNetUsers_community_images_AvatarImageId'
                  ) THEN
                    ALTER TABLE "AspNetUsers" ADD CONSTRAINT "FK_AspNetUsers_community_images_AvatarImageId"
                      FOREIGN KEY ("AvatarImageId") REFERENCES community_images (id) ON DELETE SET NULL;
                  END IF;
                END $$;
                INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES
                  ('20261126134034_NormalizeSavedRoadmapsAndCommunitySearches', '10.0.13'),
                  ('20261127151343_AccountIdentityAndPreferences', '10.0.13')
                ON CONFLICT DO NOTHING;
                """, conn);
            await cmd.ExecuteNonQueryAsync(ct);
        }, logger);
    }

    private static async Task SafeStepAsync(string stepName, Func<Task> action, ILogger logger)
    {
        try { await action(); }
        catch (Exception ex) { logger.LogError(ex, "Startup schema reconciliation step {Step} failed.", stepName); }
    }

    private static async Task SeedShowcaseIfEmptyCoreAsync(AppDb db, ILogger logger, CancellationToken ct)
    {
        if (await db.Jobs.AnyAsync(x => x.Status == "published", ct) ||
            await db.CommunityPosts.AnyAsync(x => x.Status == "published", ct))
            return;

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(8842109)", ct);

        if (await db.Jobs.AnyAsync(x => x.Status == "published", ct) ||
            await db.CommunityPosts.AnyAsync(x => x.Status == "published", ct))
            return;

        var now = DateTimeOffset.UtcNow;
        var deadline = now.AddDays(60);
        var hasher = new PasswordHasher<AppUser>();

        foreach (var (roleName, normalizedRole) in new[] { ("Candidate", "CANDIDATE"), ("Recruiter", "RECRUITER"), ("Admin", "ADMIN") })
        {
            if (!await db.Roles.AnyAsync(r => r.NormalizedName == normalizedRole, ct))
            {
                db.Roles.Add(new IdentityRole<Guid>
                {
                    Id = Guid.NewGuid(),
                    Name = roleName,
                    NormalizedName = normalizedRole,
                    ConcurrencyStamp = Guid.NewGuid().ToString("N")
                });
            }
        }
        await db.SaveChangesAsync(ct);

        var recruiterRole = await db.Roles.SingleAsync(r => r.NormalizedName == "RECRUITER", ct);
        var candidateRole = await db.Roles.SingleAsync(r => r.NormalizedName == "CANDIDATE", ct);

        const string recEmail = "tuyendung.demo@careergapai.vercel.app";
        const string autEmail = "minhkhoa.demo@careergapai.vercel.app";

        var recruiter = await db.Users.SingleOrDefaultAsync(u => u.NormalizedEmail == recEmail.ToUpperInvariant(), ct);
        if (recruiter is null)
        {
            recruiter = new AppUser
            {
                Id = Guid.Parse("11111111-2222-4333-8444-555555555501"),
                AccountStatus = "active",
                DisplayName = "Trần Thu Hà",
                UserName = recEmail,
                NormalizedUserName = recEmail.ToUpperInvariant(),
                Email = recEmail,
                NormalizedEmail = recEmail.ToUpperInvariant(),
                EmailConfirmed = true,
                SecurityStamp = Guid.NewGuid().ToString("N"),
                ConcurrencyStamp = Guid.NewGuid().ToString("N"),
                LockoutEnabled = true,
                CreatedAt = now
            };
            recruiter.PasswordHash = hasher.HashPassword(recruiter, Convert.ToHexString(RandomNumberGenerator.GetBytes(16)) + "Aa1!");
            db.Users.Add(recruiter);
            await db.SaveChangesAsync(ct);
        }
        if (!await db.UserRoles.AnyAsync(ur => ur.UserId == recruiter.Id && ur.RoleId == recruiterRole.Id, ct))
            db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = recruiter.Id, RoleId = recruiterRole.Id });

        var author = await db.Users.SingleOrDefaultAsync(u => u.NormalizedEmail == autEmail.ToUpperInvariant(), ct);
        if (author is null)
        {
            author = new AppUser
            {
                Id = Guid.Parse("11111111-2222-4333-8444-555555555502"),
                AccountStatus = "active",
                DisplayName = "Nguyễn Minh Khoa",
                UserName = autEmail,
                NormalizedUserName = autEmail.ToUpperInvariant(),
                Email = autEmail,
                NormalizedEmail = autEmail.ToUpperInvariant(),
                EmailConfirmed = true,
                SecurityStamp = Guid.NewGuid().ToString("N"),
                ConcurrencyStamp = Guid.NewGuid().ToString("N"),
                LockoutEnabled = true,
                CreatedAt = now
            };
            author.PasswordHash = hasher.HashPassword(author, Convert.ToHexString(RandomNumberGenerator.GetBytes(16)) + "Aa1!");
            db.Users.Add(author);
            await db.SaveChangesAsync(ct);
        }
        if (!await db.UserRoles.AnyAsync(ur => ur.UserId == author.Id && ur.RoleId == candidateRole.Id, ct))
            db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = author.Id, RoleId = candidateRole.Id });

        if (!await db.CandidateProfiles.AnyAsync(p => p.UserId == author.Id, ct))
        {
            db.CandidateProfiles.Add(new CandidateProfile
            {
                UserId = author.Id,
                DisplayName = "Nguyễn Minh Khoa",
                Headline = "Kỹ sư Frontend React & TypeScript",
                Summary = "Tập trung tối ưu Core Web Vitals, chuẩn trợ năng WCAG 2.2 AA và kiến trúc giao diện hiện đại.",
                Location = "Hồ Chí Minh",
                DesiredRole = "Senior Frontend Engineer",
                PreferredWorkMode = "hybrid",
                ExperienceYears = 3m,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
        await db.SaveChangesAsync(ct);

        var company = await db.Companies.FirstOrDefaultAsync(c => c.OwnerUserId == recruiter.Id && c.ArchivedAt == null, ct);
        if (company is null)
        {
            company = new Company
            {
                Id = Guid.Parse("11111111-2222-4333-8444-555555555503"),
                OwnerUserId = recruiter.Id,
                Name = "CareerGap Tech Labs",
                Slug = "careergap-tech-labs-11111111",
                Description = "Đội ngũ phát triển nền tảng tuyển dụng và phân tích năng lực nghề nghiệp dựa trên bằng chứng xác thực.",
                WebsiteUrl = "https://careergapai.vercel.app",
                Location = "Hồ Chí Minh",
                Industry = "Công nghệ thông tin",
                SizeLabel = "50–150 nhân sự",
                RowVersion = 1,
                CreatedAt = now,
                UpdatedAt = now
            };
            db.Companies.Add(company);
            await db.SaveChangesAsync(ct);
        }

        if (!await db.BusinessIdentities.AnyAsync(b => b.UserId == recruiter.Id, ct))
        {
            db.BusinessIdentities.Add(new BusinessIdentity
            {
                UserId = recruiter.Id,
                CompanyId = company.Id,
                LegalName = "Công ty TNHH Công nghệ CareerGap Việt Nam",
                TaxCode = "0318924501",
                BoundTaxCode = "0318924501",
                Domain = "careergapai.vercel.app",
                Status = "verified",
                ReviewReason = "Hồ sơ doanh nghiệp mẫu đã xác minh chính thức.",
                RowVersion = 2,
                SubmittedAt = now,
                ReviewedAt = now
            });
            await db.SaveChangesAsync(ct);
        }

        await SeedPublishedJobAsync(db, company.Id, recruiter.Id, deadline, now,
            jobId: Guid.Parse("11111111-2222-4333-8444-555555555504"),
            versionId: Guid.Parse("11111111-2222-4333-8444-555555555505"),
            tags: ["CSS", "Git", "HTML", "JavaScript", "React", "TypeScript"],
            maxApplications: 50,
            content: new JobContentRequest(
                Title: "Kỹ sư Frontend React & TypeScript (Mid-Level)",
                Description: "Tham gia phát triển giao diện ứng dụng web hiệu năng cao phục vụ phân tích hồ sơ và kết nối cơ hội nghề nghiệp. Phối hợp chặt chẽ cùng đội ngũ sản phẩm và Backend để tối ưu Core Web Vitals, khả năng truy cập chuẩn WCAG 2.2 AA và trải nghiệm trên thiết bị di động.",
                RequirementsText: string.Join("\n", [
                    "Yêu cầu bắt buộc:",
                    "- Tối thiểu 2 năm kinh nghiệm phát triển ứng dụng web với React.",
                    "- Thành thạo TypeScript trong dự án thực tế.",
                    "- Nắm vững HTML5 ngữ nghĩa, CSS hiện đại và tối ưu hiệu năng hiển thị.",
                    "- Sử dụng thành thạo Git trong quy trình làm việc nhóm.",
                    "Ưu tiên:",
                    "- Ưu tiên ứng viên có kinh nghiệm viết kiểm thử tự động với Vitest hoặc Playwright.",
                    "- Là một lợi thế nếu hiểu rõ tiêu chuẩn trợ năng WCAG."
                ]),
                BenefitsText: "Lương tháng 13, bảo hiểm sức khỏe toàn diện, phụ cấp thiết bị làm việc và ngân sách học tập hàng năm.",
                Industry: "Công nghệ thông tin",
                Location: "Hồ Chí Minh",
                EmploymentType: "full_time",
                WorkMode: "hybrid",
                ExperienceMin: 2m,
                ExperienceMax: 4m,
                SalaryMin: 25000000m,
                SalaryMax: 40000000m,
                SalaryCurrency: "VND",
                SalaryPeriod: "month",
                Language: "vi"), ct);

        await SeedPublishedJobAsync(db, company.Id, recruiter.Id, deadline, now,
            jobId: Guid.Parse("11111111-2222-4333-8444-555555555506"),
            versionId: Guid.Parse("11111111-2222-4333-8444-555555555507"),
            tags: [".NET", "ASP.NET Core", "C#", "Docker", "PostgreSQL", "REST API"],
            maxApplications: 40,
            content: new JobContentRequest(
                Title: "Kỹ sư Backend .NET & PostgreSQL",
                Description: "Thiết kế và vận hành hệ thống REST API trên nền tảng ASP.NET Core 10 và cơ sở dữ liệu PostgreSQL 16. Đảm bảo tính toàn vẹn giao dịch, kiểm soát truy cập theo vai trò, tối ưu truy vấn và độ tin cậy của hàng đợi xử lý tài liệu.",
                RequirementsText: string.Join("\n", [
                    "Yêu cầu bắt buộc:",
                    "- Tối thiểu 2 năm kinh nghiệm phát triển dịch vụ Backend với C# và ASP.NET Core.",
                    "- Thành thạo thiết kế lược đồ, chỉ mục và truy vấn SQL trên PostgreSQL.",
                    "- Có kinh nghiệm xây dựng REST API bảo mật, xử lý đồng thời và kiểm soát giao dịch.",
                    "- Quen thuộc đóng gói ứng dụng với Docker.",
                    "Ưu tiên:",
                    "- Ưu tiên có kinh nghiệm vận hành CI/CD và giám sát hiệu năng hệ thống.",
                    "- Là lợi thế nếu từng làm việc với lưu trữ đối tượng tương thích S3."
                ]),
                BenefitsText: "Làm việc từ xa linh hoạt, đánh giá lương định kỳ 2 lần/năm, hỗ trợ chi phí chứng chỉ chuyên môn.",
                Industry: "Công nghệ thông tin",
                Location: "Hà Nội",
                EmploymentType: "full_time",
                WorkMode: "remote",
                ExperienceMin: 2m,
                ExperienceMax: 5m,
                SalaryMin: 28000000m,
                SalaryMax: 45000000m,
                SalaryCurrency: "VND",
                SalaryPeriod: "month",
                Language: "vi"), ct);

        if (!await db.CommunityProfiles.AnyAsync(p => p.UserId == author.Id, ct))
        {
            db.CommunityProfiles.Add(new CommunityProfile
            {
                UserId = author.Id,
                Handle = "minhkhoa_dev",
                DisplayName = "Nguyễn Minh Khoa",
                Bio = "Kỹ sư Frontend chia sẻ kinh nghiệm tối ưu hiệu năng web và chuẩn bị hồ sơ kỹ thuật.",
                AllowMentions = true,
                ShowCareer = false,
                RowVersion = 1,
                UpdatedAt = now
            });
        }
        if (!await db.CommunityProfiles.AnyAsync(p => p.UserId == recruiter.Id, ct))
        {
            db.CommunityProfiles.Add(new CommunityProfile
            {
                UserId = recruiter.Id,
                Handle = "thuha_tech",
                DisplayName = "Trần Thu Hà",
                Bio = "Quản lý tuyển dụng kỹ thuật tại CareerGap Tech Labs.",
                AllowMentions = true,
                ShowCareer = true,
                RowVersion = 1,
                UpdatedAt = now
            });
        }
        await db.SaveChangesAsync(ct);

        var post1Id = Guid.Parse("11111111-2222-4333-8444-555555555508");
        var post2Id = Guid.Parse("11111111-2222-4333-8444-555555555509");

        if (!await db.CommunityPosts.AnyAsync(p => p.Id == post1Id, ct))
        {
            db.CommunityPosts.Add(new CommunityPost
            {
                Id = post1Id,
                AuthorId = author.Id,
                Title = "Cách trình bày bằng chứng số liệu trong CV kỹ sư phần mềm",
                Slug = "cach-trinh-bay-bang-chung-so-lieu-trong-cv-1111111108",
                Summary = "Thay vì liệt kê danh sách công nghệ chung chung, hãy gắn mỗi dự án với bối cảnh bài toán, hành động kỹ thuật cụ thể và kết quả đo lường được.",
                Body = string.Join("\n\n", [
                    "Khi rà soát hàng trăm CV ứng tuyển vị trí Frontend và Backend, điểm khác biệt lớn nhất giữa một hồ sơ nổi bật và hồ sơ mờ nhạt nằm ở **bằng chứng xác thực**.",
                    "### 1. Nêu rõ phạm vi và vai trò thực tế",
                    "- Ghi rõ quy mô hệ thống hoặc nhóm phát triển bạn trực tiếp tham gia.",
                    "- Tách bạch phần việc do chính bạn thiết kế, triển khai so với phần dùng sẵn từ nền tảng.",
                    "### 2. Gắn kỹ năng với kết quả đo được",
                    "- Thay vì chỉ ghi `React, TypeScript`, hãy mô tả cụ thể: tách gói mã nguồn giúp giảm dung lượng tải ban đầu từ `825 KB` xuống `226 KB` và đưa chỉ số `CLS` về `0.00`.",
                    "- Với vị trí Backend, hãy nêu rõ cách bạn dùng khóa giao dịch hoặc chỉ mục PostgreSQL để ngăn lỗi ghi trùng dưới tải thực tế.",
                    "### 3. Giữ thông tin trung thực và kiểm chứng được",
                    "Mọi gạch đầu dòng trong CV nên sẵn sàng để bạn giải thích sâu về đánh đổi kỹ thuật trong buổi phỏng vấn."
                ]),
                Category = "career",
                Status = "published",
                RowVersion = 2,
                CreatedAt = now,
                UpdatedAt = now,
                PublishedAt = now
            });
        }

        if (!await db.CommunityPosts.AnyAsync(p => p.Id == post2Id, ct))
        {
            db.CommunityPosts.Add(new CommunityPost
            {
                Id = post2Id,
                AuthorId = recruiter.Id,
                Title = "Kinh nghiệm chuẩn bị phỏng vấn hệ thống .NET và PostgreSQL thực tế",
                Slug = "kinh-nghiem-chuan-bi-phong-van-he-thong-net-1111111109",
                Summary = "Những chủ đề cốt lõi thường được hỏi trong vòng phỏng vấn kỹ thuật Backend: thiết kế lược đồ, kiểm soát đồng thời, bảo mật phiên và kiểm thử tích hợp.",
                Body = string.Join("\n\n", [
                    "Trong các buổi phỏng vấn vị trí Kỹ sư Backend `.NET` và `PostgreSQL`, hội đồng kỹ thuật thường đánh giá cao khả năng hiểu bản chất hệ thống hơn là thuộc lòng cú pháp.",
                    "### Các nhóm câu hỏi trọng tâm nên ôn luyện",
                    "- **Toàn vẹn dữ liệu & đồng thời:** Khi nào cần khóa bi quan `SELECT ... FOR UPDATE`, khi nào dùng `RowVersion` lạc quan để tránh mất cập nhật.",
                    "- **Hiệu năng truy vấn:** Đọc `EXPLAIN ANALYZE`, chọn chỉ mục phù hợp cho truy vấn lọc phân trang và tránh lỗi `N+1` trong Entity Framework Core.",
                    "- **Bảo mật ứng dụng web:** Cơ chế xác thực cookie `HttpOnly`, chống giả mạo yêu cầu `CSRF` và phân quyền ở tầng dịch vụ.",
                    "- **Kiểm thử đáng tin cậy:** Viết kiểm thử tích hợp chạy trên cơ sở dữ liệu PostgreSQL thật trong Docker thay vì chỉ dùng bộ nhớ giả lập."
                ]),
                Category = "interview",
                Status = "published",
                RowVersion = 2,
                CreatedAt = now,
                UpdatedAt = now,
                PublishedAt = now
            });
        }
        await db.SaveChangesAsync(ct);

        foreach (var tag in new[] { "react", "typescript", "cv", "career", ".net", "postgresql", "docker", "interview" })
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO community_tags (name) VALUES ({tag}) ON CONFLICT DO NOTHING", ct);

        foreach (var (pid, tag) in new[]
        {
            (post1Id, "react"), (post1Id, "typescript"), (post1Id, "cv"), (post1Id, "career"),
            (post2Id, ".net"), (post2Id, "postgresql"), (post2Id, "docker"), (post2Id, "interview")
        })
        {
            if (!await db.CommunityPostTags.AnyAsync(pt => pt.PostId == pid && pt.Tag == tag, ct))
                db.CommunityPostTags.Add(new CommunityPostTag { PostId = pid, Tag = tag });
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        logger.LogInformation("Seeded showcase company, 2 published jobs, and 2 community posts.");
    }

    private static async Task SeedPublishedJobAsync(AppDb db, Guid companyId, Guid ownerId, DateTimeOffset deadline, DateTimeOffset now,
        Guid jobId, Guid versionId, string[] tags, int maxApplications, JobContentRequest content, CancellationToken ct)
    {
        if (await db.Jobs.AnyAsync(j => j.Id == jobId, ct)) return;

        var contentHash = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(content)));
        var job = new JobPosting
        {
            Id = jobId,
            CompanyId = companyId,
            Status = "draft",
            ApplicationDeadline = deadline,
            MaxApplications = maxApplications,
            Tags = tags,
            RowVersion = 1,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Jobs.Add(job);
        await db.SaveChangesAsync(ct);

        var version = new JobVersion
        {
            Id = versionId,
            JobId = jobId,
            CreatedBy = ownerId,
            VersionNo = 1,
            Title = content.Title!,
            Description = content.Description!,
            RequirementsText = content.RequirementsText!,
            BenefitsText = content.BenefitsText,
            Industry = content.Industry,
            Location = content.Location,
            EmploymentType = content.EmploymentType!,
            WorkMode = content.WorkMode!,
            ExperienceMin = content.ExperienceMin,
            ExperienceMax = content.ExperienceMax,
            SalaryMin = content.SalaryMin,
            SalaryMax = content.SalaryMax,
            SalaryCurrency = content.SalaryCurrency,
            SalaryPeriod = content.SalaryPeriod,
            Language = content.Language!,
            ContentHash = contentHash,
            CreatedAt = now
        };
        db.JobVersions.Add(version);
        await db.SaveChangesAsync(ct);

        job.Status = "published";
        job.CurrentVersionId = version.Id;
        job.PublishedAt = now;
        job.RowVersion = 2;
        job.UpdatedAt = now;

        if (JobRevisionFactory.TryExtract(version.Id, null, 1, version.RequirementsText, now, out var extracted, out _) && extracted is not null)
        {
            db.JobDataRevisions.Add(extracted);
            using var doc = JsonDocument.Parse(extracted.StructuredData);
            var selections = doc.RootElement.GetProperty("requirements").EnumerateArray()
                .Select(r => new JobRequirementSelection(
                    r.GetProperty("requirementId").GetString()!,
                    r.GetProperty("importance").GetString() is "required" or "optional" ? r.GetProperty("importance").GetString()! : "required"))
                .ToArray();
            if (JobRevisionFactory.TryConfirm(extracted, ownerId, 2, new ConfirmJobRevisionRequest(1, selections, []), now, out var confirmed, out _) && confirmed is not null)
                db.JobDataRevisions.Add(confirmed);
        }
        await db.SaveChangesAsync(ct);
    }
}
