# Taskboard — CareerGap AI

> **Ngày đồng bộ:** 20/11/2026  
> **Trạng thái Baseline hiện tại:** `final_defense_ready` (`release20261031` — đã nghiệm thu nội bộ M6 ngày 01/11/2026, đóng băng 222 file nguồn).  
> **Nguồn đồng bộ:** `project-docs/current-status.md`, `project-docs/report/m6-internal-acceptance-2026-11-01.md`, `output/mvc-feature-backlog-20261102/README.md`, `output/feature-comparison-20261102/feature-gap-report.md`.

---

## Phần 1: Bảo vệ đồ án & Kiểm chứng thực tế (Ngoài phạm vi đóng băng)

Các hạng mục này không chặn nghiệm thu nội bộ M6, nhưng cần hoàn tất cho buổi bảo vệ chính thức hoặc nếu phát triển tiếp lên bản Production:

- [ ] **Bảo vệ chính thức trước Hội đồng:** Ghi nhận kết quả, nhận xét và điểm đánh giá thực tế tại buổi bảo vệ.
- [ ] **Đánh giá người dùng thật (Human Evaluation):** Hiện `human_direct_label_review = 0` và `external_human_review = 0` (mới có đối chiếu kỹ thuật bởi Codex trên 318 nhãn).
- [ ] **Mở rộng tập dữ liệu tiếng Việt & đại diện:** Pilot hiện tại chỉ có `2 CV Anh × 4 JD` (`F1 = 0.623` exact, `nDCG@3 = 0.917` chưa vượt baseline `1.0`). Cần bổ sung CV tiếng Việt gốc và đo `Partial P/R/F1` nếu nâng cấp mô hình AI.
- [ ] **Kiểm chứng nội dung video học tập tiếng Việt (A13):** Đã kiểm tra HTTP 200 và metadata, nhưng chưa phát xem toàn bộ nội dung video tiếng Việt.

---

## Phần 2: Backlog tính năng chưa hoàn thành (32 đầu việc từ đối chiếu MVC)

> **Cập nhật 20/11/2026 (Phương án B):** Đã đồng bộ mã nguồn mở rộng từ worktree `D:/CareerGapAI-community-20261102` (bao gồm phân hệ Cộng đồng, Xác thực doanh nghiệp, **10/10 mục P1** và **8 mục P2** `F15, F16, F20, F21, F22, F24, F29, F30`) về `D:/CareerGapAI`. Toàn bộ mục đã đồng bộ giữ trạng thái `[ ]` / `[~]` **chờ chủ dự án xác nhận nghiệm thu** (không ghi đè baseline `release20261031` đã đóng băng).

### Ưu tiên P1 — Hoàn thiện luồng chính (10/10 Đã nghiệm thu — 20/11/2026)

> **Quy ước trạng thái:** `[x]` = Đã triển khai & được chủ dự án xác nhận nghiệm thu · `[ ]` = Độc lập, chưa nghiệm thu · `[~]` = Có phụ thuộc.

- [x] **F01** *(Học tập · Đã nghiệm thu 20/11/2026)* — **Lộ trình học theo tuần:** Tổ chức kỹ năng còn thiếu thành các tuần (`WeeklyRoadmap.cs`, `weeklyRoadmap.tsx`). *Pass `dotnet build`, `WeeklyRoadmapPlanTests` (2/2), `vite build` & kiểm tra thực tế trên UI.*
- [x] **F02** *(Học tập · Đã nghiệm thu 20/11/2026)* — **Lưu và theo dõi tiến độ học:** Lưu `SavedRoadmap.cs`, migration `SavedWeeklyRoadmaps` và UI checkbox tiến độ (`weeklyRoadmap.tsx`). *Pass `dotnet build`, `vite build` & kiểm tra thực tế trên UI.*
- [x] **F07** *(AI & Phân tích · Đã nghiệm thu 20/11/2026)* — **Trung tâm lịch sử phân tích:** `AnalysisHistory.cs` (`GET /api/matching/history`), view `#analysis-history` (`analysisHistory.tsx`) và kiểm quyền `CanAccess`. *Pass `dotnet build`, `navigation.test.ts` (11/11), `vite build` & kiểm tra thực tế trên UI.*
- [x] **F10** *(Tuyển dụng · Đã nghiệm thu 20/11/2026)* — **Hoàn thiện nhập, sửa và hiển thị mức lương:** `salary.ts`, form lương & thẻ tin trong `jobs.tsx`, bảo toàn lương khi tạo version JD mới. *Pass `salary.test.ts` (2/2), `dotnet build`, `vite build` & kiểm tra thực tế trên UI.*
- [x] **F11** *(Tuyển dụng · Đã nghiệm thu 20/11/2026)* — **Thư ứng tuyển / Cover Letter:** Cột `cover_letter`, migration `ApplicationCoverLetter`, `ApplicationEndpoints.cs`, `coverLetter.ts` và form/xem thư trong `jobs.tsx`. *Pass `coverLetter.test.ts` (3/3), `dotnet build`, `vite build` & kiểm tra thực tế trên UI.*
- [x] **F13** *(Tuyển dụng · Đã nghiệm thu 20/11/2026)* — **Trang chi tiết việc làm & liên kết chia sẻ:** `jobLinks.ts` (`#jobs?job=<slug>-<guid>`), chi tiết job công khai và kiểm tra trạng thái lưu/ứng tuyển theo `jobId` trong `jobs.tsx`. *Pass `jobLinks.test.ts` (3/3), `dotnet build`, `vite build` & kiểm tra thực tế trên UI.*
- [x] **F17** *(Tuyển dụng · Đã nghiệm thu 20/11/2026)* — **Bộ lọc và phân trang quản lý tin của Recruiter:** Lọc `status` trên `GET /api/jobs/mine` và UI phân trang/lọc trong `RecruiterJobManager` (`jobs.tsx`). *Pass `dotnet build`, `vite build` & kiểm tra thực tế trên UI.*
- [x] **F18** *(Tuyển dụng · Đã nghiệm thu 20/11/2026)* — **Bộ lọc và phân trang danh sách hồ sơ gửi đến:** Lọc `status` trên `GET /api/jobs/{jobId}/applications` và UI phân trang ứng viên trong `jobs.tsx`. *Pass `dotnet build`, `vite build` & kiểm tra thực tế trên UI.*
- [x] **F23** *(Thông báo · Đã nghiệm thu 20/11/2026)* — **Hoàn thiện trung tâm thông báo:** `NotificationEndpoints.cs` (`/api/notifications/inbox`, `read-all`), `notificationLinks.ts` và `recruitmentNotifications.tsx`. *Pass `notificationLinks.test.ts` (1/1), `dotnet build`, `vite build` & kiểm tra thực tế trên UI.*
- [x] **F26** *(Quản trị · Đã nghiệm thu 20/11/2026)* — **Quy trình báo cáo tin tuyển dụng đầy đủ:** Migration `ReportReviewWorkflow`, `ReportEndpoints.cs`, `reportModeration.tsx` và `administration.tsx`. *Pass `dotnet build`, `vite build` & kiểm tra thực tế trên UI.*

### Ưu tiên P2 — Mở rộng chiều sâu & Nghiệp vụ (19/19 Đã nghiệm thu — 20/11/2026)

- [x] **F03** *(Học tập · Đã nghiệm thu 20/11/2026 · Phụ thuộc F01, F05)* — **Bối cảnh nghề nghiệp:** `CareerContext` (`career_start` / `career_switch` / `promotion`) trên `MatchInput`, `WeeklyRoadmap.cs`, `pairMatching.tsx`, `weeklyRoadmap.tsx`. *Pass `StageP2Batch4Tests` PG, `vite build` & kiểm tra UI.*
- [x] **F04** *(AI & Phân tích · Đã nghiệm thu 20/11/2026 · Phụ thuộc F05)* — **Nhập JD từ URL ngoài hệ thống:** `JdUrlImporter.cs` (`POST /api/matching/jd-url-preview`, chống SSRF, timeout 8s, giới hạn 512 KB) và `quickMatch.tsx`. *Pass `StageP2Batch4Tests` PG, `vite build` & kiểm tra UI.*
- [x] **F05** *(AI & Phân tích · Đã nghiệm thu 20/11/2026)* — **Form đối chiếu nhanh nhiều nguồn CV/JD:** `QuickMatchEndpoints.cs` (`POST /api/matching/quick-pairs`, `QuickMatchJd`) và `quickMatch.tsx`. *Pass `StageP2Batch4Tests` PG, `vite build` & kiểm tra UI.*
- [x] **F06** *(AI & Phân tích · Đã nghiệm thu 20/11/2026)* — **AI hỗ trợ viết lại JD:** `JobRewriteContract.cs`, `JobRewriteService.cs`, `JobRewriteWorker.cs` (dùng lại `JobService`/`JobWorker`) và `jobs.tsx`. *Pass `StageP2Batch4Tests` PG, `vite build` & kiểm tra UI.*
- [x] **F08** *(AI & Phân tích · Đã nghiệm thu 20/11/2026 · Phụ thuộc F07)* — **Ẩn/xóa lượt phân tích khỏi lịch sử:** Bảng `analysis_history_hides` (giữ nguyên `match_inputs` bất biến & sổ ngân sách), `DELETE /api/matching/history/{id}` và `analysisHistory.tsx`. *Pass `StageP2Batch4Tests` PG, `vite build` & kiểm tra UI.*
- [x] **F09** *(AI & Phân tích · Đã nghiệm thu 20/11/2026)* — **Góc nhìn tổng hợp 6 nhóm tiêu chí:** `PairCriterionGroups.cs` (read-only từ `pair-evidence-v1`, không đổi scorer) và `pairMatching.tsx`. *Pass `StageP2Batch4Tests` PG, `vite build` & kiểm tra UI.*
- [x] **F12** *(Tuyển dụng · Đã nghiệm thu 20/11/2026)* — **Giới hạn số hồ sơ đang xử lý của một tin:** `MaxApplications`, migration `JobCapacityAndSoftDelete`, khóa `FOR UPDATE` khi nộp đơn & tự trả chỗ khi rút/từ chối. *Pass `JobCapacityAndSoftDeleteTests` (1/1) PG, `vite build` & kiểm tra UI.*
- [x] **F15** *(Tuyển dụng · Đã nghiệm thu 20/11/2026)* — **Tag công nghệ/nghề nghiệp có cấu trúc:** `JobTags.cs`, migration `JobTags` và `jobTags.tsx`. *Pass `JobTagTests` (1/1) PG, `vite build` & kiểm tra UI.*
- [x] **F16** *(Tuyển dụng · Đã nghiệm thu 20/11/2026)* — **Danh mục địa điểm thống nhất:** `LocationCatalog.cs` (34 tỉnh/thành + alias) và `locationInput.tsx`. *Pass `LocationCatalogTests` (2/2) PG, `vite build` & kiểm tra UI.*
- [x] **F19** *(Tuyển dụng · Đã nghiệm thu 20/11/2026 · Phụ thuộc F17)* — **Recruiter tự xóa mềm tin:** `POST /api/jobs/{id}/archive` & `/restore` (`ArchivedByRole`, `PreviousStatus`), giữ nguyên đơn/snapshot & chặn khôi phục tin do Admin ẩn. *Pass `JobCapacityAndSoftDeleteTests` (1/1) PG, `vite build` & kiểm tra UI.*
- [x] **F20** *(Hồ sơ & Doanh nghiệp · Đã nghiệm thu 20/11/2026)* — **Tải và hiển thị logo doanh nghiệp:** `CompanyLogoEndpoints.cs`, migration `CompanyLogos` và `companyLogo.tsx`. *Pass `CompanyLogoTests` (1/1) PG, `vite build` & kiểm tra UI.*
- [x] **F21** *(Hồ sơ & Doanh nghiệp · Đã nghiệm thu 20/11/2026)* — **Danh mục kỹ năng & bộ chọn kỹ năng chuẩn:** `SkillCatalog.cs`, `skillCatalog.ts`, `skillPicker.tsx`. *Pass `SkillCatalogTests` (1/1), `skillCatalog.test.ts` (1/1), `vite build` & kiểm tra UI.*
- [x] **F22** *(Hồ sơ & Doanh nghiệp · Đã nghiệm thu 20/11/2026 · Phụ thuộc F20, F21)* — **Thẻ nghề nghiệp theo vai trò trên hồ sơ tác giả:** `CommunityCareer.cs`, migration `CommunityCareerVisibility` và `communityCareer.tsx`. *Pass `CommunityCareerTests` (4/4) PG, `vite build` & kiểm tra UI.*
- [x] **F24** *(Thông báo · Đã nghiệm thu 20/11/2026 · Phụ thuộc F23)* — **Cập nhật trực tiếp thông báo (Real-time):** `recruitmentLive.ts` và SSE `/api/community/events`. *Pass `RecruitmentLiveTests` (4/4) PG, `recruitmentLive.test.ts` (3/3), `vite build` & kiểm tra UI.*
- [x] **F25** *(Quản trị · Đã nghiệm thu 20/11/2026 · Phụ thuộc F15)* — **Dashboard số liệu và biểu đồ nghiệp vụ:** `GET /api/admin/dashboard` (số tồn hiện tại, chuỗi phát sinh theo ngày & top tag việc làm) và tab Tổng quan trong `administration.tsx`. *Pass `AdminDashboardAndFilterTests` (1/1) PG, `vite build` & kiểm tra UI.*
- [x] **F27** *(Quản trị · Đã nghiệm thu 20/11/2026)* — **Bộ lọc và thông tin quản trị tài khoản/tin:** `LastLoginAt` (migration `AdminUserLastLogin`), lọc `status`/`role`/`query` trên `/api/admin/accounts` & `/api/admin/jobs` và `administration.tsx`. *Pass `AdminDashboardAndFilterTests` (1/1) PG, `vite build` & kiểm tra UI.*
- [x] **F29** *(Cộng đồng · Đã nghiệm thu 20/11/2026)* — **Lọc kết hợp nhiều tag bài viết:** `CommunityService.cs`, `communityTagFilter.tsx`, `communityTagMatch.ts`. *Pass `CommunityTagTests` (3/3) PG, `communityTagMatch.test.ts` (2/2), `vite build` & kiểm tra UI.*
- [x] **F30** *(Cộng đồng · Đã nghiệm thu 20/11/2026)* — **Hồ sơ xử lý và bộ lọc báo cáo cộng đồng:** `CommunityReportQueries.cs` và `communityReports.tsx`. *Pass `CommunityReportTests` (5/5) PG, `vite build` & kiểm tra UI.*
- [x] **F31** *(Cộng đồng · Đã nghiệm thu 20/11/2026)* — **Trình soạn bài nâng cao:** Code block, danh sách số, gạch chân/ngang, inline code & chèn ảnh nội bộ (`/api/community/images/{id}`) trong `communityMarkdown.tsx`, `community.tsx`, `CommunityEndpoints.cs`. *Pass `communityMarkdown.test.tsx` (3/3), `CommunityBodyImageTests` (1/1) PG, `vite build` & kiểm tra UI.*

### Ưu tiên P3 — Tiện ích & Khám phá (3/3 Đã nghiệm thu — 20/11/2026)

- [x] **F14** *(Tuyển dụng · Đã nghiệm thu 20/11/2026 · Phụ thuộc F13)* — **Lượt xem tin tuyển dụng:** Bảng `job_views`, khử trùng theo `userId`/cookie mỗi ngày trên `GET /api/jobs/{id}`, trả `viewCount` trong `JobDto` và hiển thị ở `jobs.tsx`. *Pass `StageP3DiscoveryTests` (1/1) PG, `vite build` & kiểm tra UI.*
- [x] **F28** *(Khám phá · Đã nghiệm thu 20/11/2026 · Phụ thuộc F13, F15, F16)* — **Trang chủ công khai tổng hợp:** `publicHome.tsx` (`#home` trong `navigation.ts`, `workspace.tsx`, `main.tsx`) tổng hợp tìm kiếm nhanh, tag/địa điểm, tin mới nhất, Skill Gap & bài viết cộng đồng. *Pass `navigation.test.ts` (12/12), `vite build` & kiểm tra UI.*
- [x] **F32** *(Cộng đồng · Đã nghiệm thu 20/11/2026)* — **Thống kê/gợi ý từ khóa được tìm nhiều:** Bảng `community_searchs`, PII guard + chỉ ghi nhận từ khóa khớp bài `published` (`CommunityService.cs`, `CommunityEndpoints.cs`) và chip xu hướng trong `community.tsx`. *Pass `StageP3DiscoveryTests` (1/1) PG, `vite build` & kiểm tra UI.*

---

## Phần 3: Đồng bộ Git, Worktree & Dọn dẹp Workspace

- [~] **Xác nhận đồng bộ nhánh cộng đồng vào Git & Commit** *(21/11/2026 — Chờ chủ dự án xác nhận nghiệm thu)*: Đã kiểm thử hồi quy toàn phần sau đồng bộ trên PostgreSQL 16 (`dotnet test`: **231/231 passed** với provider `Fake`, đã cập nhật 2 test migration cũ trong `PairMigrationTests.cs` [27→31 migrations] và `FoundationTests.cs` [chèn user tương thích schema `Foundation`]) và Frontend (`vitest`: **11 file / 42 test passed**, `typecheck`, `lint`, `build` đều đạt; không chạy `e2e` theo xác nhận). Đã đồng bộ lên nhánh `feat/post-freeze-p1-p3-20261121` (từ `41b82c6`, giữ nguyên worktree `D:/CareerGapAI-community-20261102`, không `git push`).
- [~] **Kiểm tra & dọn dẹp file tạm trong workspace** *(21/11/2026 — Chờ chủ dự án xác nhận nghiệm thu)*:
  - Đã xóa 3 file khóa/tạm Word (`project-docs/~$] Phieu theo doi tien do DACN.doc`, `project-docs/~WRL0001.tmp`, `project-docs/~WRL0002.tmp`) và cây thư mục rỗng `tmp/` (`tmp/pdfs/`).
  - Đã cập nhật `.gitignore` để bỏ qua `design-ref/`, `output/`, `frontend/topcv-design/`, `.agents/`, `docs/reviews/`, `frontend/logo_careergapai.jpg`; giữ tracked `project-docs/proposals/` và các báo cáo/manifest trong `project-docs/report/` & `project-docs/evaluation/`.
- [~] **Cập nhật phiên bản khi sửa code sau đóng băng** *(21/11/2026 — Chờ chủ dự án xác nhận nghiệm thu)*: Đã gắn annotated tag cục bộ `v2026.11.21-p1-p3` trên nhánh `feat/post-freeze-p1-p3-20261121`, không ghi đè lên gói Defense Baseline `release20261031`.
