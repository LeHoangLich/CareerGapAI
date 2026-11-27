# Taskboard v2 — Tinh chỉnh Đồng bộ Toàn Trang Web, Sửa Lỗi Logic & Khắc Phục Hạn Chế (CareerGap AI)

> **Ngày lập:** 21/11/2026  
> **Nhánh áp dụng:** `feat/post-freeze-p1-p3-20261121` (tag `v2026.11.21-p1-p3`, 31 migrations)  
> **Nguyên tắc bảo toàn:** Không sửa hoặc ghi đè gói **Defense Baseline `release20261031`** (`41b82c6`, 222 file đóng băng, 19 migrations). Mọi thay đổi trên nhánh mở rộng phải có kiểm thử hồi quy mới đi kèm.  
> **Thống kê tổng quan:** **56 đầu việc** chia thành 5 nhóm (`BE-01..14`, `FE-01..13`, `UI-01..08`, `SYNC-01..14`, `LIM-01..07`).

---

## Quy ước mức độ ưu tiên
- **P0 (Khẩn cấp — Chặn luồng / Rò rỉ quyền / Lỗi bảo mật hoặc treo nền):** Phải xử lý ngay trước khi demo/mở rộng.
- **P1 (Quan trọng — Lỗi logic trải nghiệm / Đứt luồng / Lệch giao diện rõ rệt giữa các cửa sổ):** Cần xử lý trong đợt tinh chỉnh chính.
- **P2 (Trung bình — Chuẩn hóa đồng bộ API, form controls, responsiveness, accessibility chi tiết):** Nâng chất lượng đồng bộ toàn hệ thống.
- **P3 (Thấp / Nợ kỹ thuật & Hạn chế dài hạn — Quy ước schema, vận hành, đánh giá AI ngoài phạm vi local):** Lên kế hoạch xử lý theo lộ trình.

---

## Nhóm 1 — Sửa lỗi Logic, Bảo mật & Toàn vẹn dữ liệu Backend (`BE-01` .. `BE-14`)

- [x] **BE-01 (P0)** — **Chặn nộp đơn ứng tuyển vào tin của nhà tuyển dụng đang bị khóa (`suspended`)**
  - **Vị trí:** `backend/CareerGap.Api/Applications/ApplicationEndpoints.cs:66-82`
  - **Mô tả vấn đề:** `SubmitAsync` hiện kiểm tra `Company.ArchivedAt == null`, `BusinessIdentityEndpoints.Verified(...)` và `job.Status == "published"`, nhưng **không kiểm tra `owner.AccountStatus == "active"`**. Dù `GET /api/jobs` đã ẩn tin của recruiter bị khóa, ứng viên có `jobId` vẫn gọi `POST /api/jobs/{jobId}/applications` thành công.
  - **Yêu cầu tinh chỉnh:** Join/kiểm tra `owner.AccountStatus == "active"` trong transaction nộp đơn; từ chối (`404`/`409`) nếu chủ công ty đang `suspended` hoặc không ở trạng thái `active`.
  - **Kiểm chứng:** Bổ sung test trong `ApplicationTests.cs` xác nhận ứng viên bị chặn khi nộp đơn vào tin của recruiter đã bị Admin suspend.
  - **Trạng thái:** Đã triển khai & pass test (`BE01_CandidateCannotApplyToJobOfSuspendedRecruiter`) — Đã nghiệm thu bởi chủ dự án (21/11/2026).

- [x] **BE-02 (P0)** — **Bổ sung xóa/ẩn danh dữ liệu P2/P3 khi purge tài khoản vĩnh viễn**
  - **Vị trí:** `backend/CareerGap.Api/Accounts/AccountDeletionService.cs:258-409`, `backend/CareerGap.Api/Data/AppDb.cs:142-144,320-338`
  - **Mô tả vấn đề:** `PurgeDatabaseRowsAsync` ẩn danh hóa `AppUser` và giữ lại `AiJob` để đối soát chi phí (không xóa row), nên `DeleteBehavior.Cascade` trên `QuickMatchJd`, `JobRewriteInput`, `JobRewriteResult` không tự kích hoạt. Dữ liệu `QuickMatchJds` (`RawText`, `Title`, `SourceUrl`, `RequirementsJson`), `JobRewriteInputs.Payload`, `JobRewriteResults.Sections`, `CommunitySearches`, `JobViews`, và `LearningRequests.Topic`/`InputHash` vẫn còn nguyên sau khi purge.
  - **Yêu cầu tinh chỉnh:** Cập nhật `PurgeDatabaseRowsAsync` để xóa hoặc ẩn danh toàn bộ các bảng P2/P3 trên (giữ lại `AiJob`/`BudgetReservation`/`ExternalCallAttempt` phục vụ đối soát chi phí nhưng xóa sạch payload văn bản) và dọn bản ghi hash `Viewer` trong `CommunitySearches`/`JobViews` tương tự `CommunityViews`.
  - **Kiểm chứng:** Mở rộng `AccountDeletionTests.cs` kiểm chứng toàn bộ bảng P2/P3 rỗng/ẩn danh sau khi worker purge chạy xong.
  - **Trạng thái:** Đã triển khai & pass test (`BE02_AccountPurgeRemovesP2P3QuickMatchRewriteHistoryViewsSearchesAndLearningTopic`) — Đã nghiệm thu bởi chủ dự án (21/11/2026).

- [x] **BE-03 (P0)** — **Bịt lỗ hổng kiểm tra quyền `rewrite_jd` tại `GET /api/ai/jobs/{id}`**
  - **Vị trí:** `backend/CareerGap.Api/Ai/JobService.cs:381-398`
  - **Mô tả vấn đề:** `GET /api/ai/jobs/{id}` ủy quyền đọc/kiểm quyền cho `suggest_cv` (`writingService.Read`) và `match_pair` (`pairService.Read`), nhưng thiếu nhánh cho `rewrite_jd`. Recruiter vẫn nhận `OutputJson` thô qua endpoint chung kể cả khi tin hoặc công ty đã bị archive (trong khi `/api/writing/jd-proposals/{id}` trả `404`).
  - **Yêu cầu tinh chỉnh:** Thêm nhánh `rewrite_jd` trong `GET /api/ai/jobs/{id}` gọi `jobRewriteService.Read(...)` để áp dụng `CanAccess` và trả DTO chuẩn.
  - **Kiểm chứng:** Test chứng minh tin/công ty đã archive thì `GET /api/ai/jobs/{id}` trả `404` tương đồng `/api/writing/jd-proposals/{id}`.
  - **Trạng thái:** Đã triển khai & pass test (`BE03_AiJobsEndpointEnforcesJobRewriteAccessAndProjectsStructuredDto`) — Đã nghiệm thu bởi chủ dự án (21/11/2026).

- [x] **BE-04 (P1)** — **Ngăn tạo `Resume`/`StoredFile`/`QuickMatchJd` rác khi `QuickMatch` lỗi đầu vào hoặc hết quota AI**
  - **Vị trí:** `backend/CareerGap.Api/Matching/QuickMatchEndpoints.cs:103-196`
  - **Mô tả vấn đề:** Khi `CvSource == "paste"`, code ghi file ra storage và `SaveChangesAsync` tạo `StoredFile`, `Resume`, `ResumeVersion`, `ResumeDataRevision` ("CV đối chiếu nhanh") *trước* khi kiểm tra JD hoặc gọi `jobs.EnqueueMatch(...)`. Nếu lỗi 400/422 hoặc bị chặn quota 429/503, CV rác và `QuickMatchJd` mồ côi vẫn lưu vĩnh viễn.
  - **Yêu cầu tinh chỉnh:** Validate toàn bộ CV + JD + kiểm tra quyền/khả dụng trước khi ghi DB/storage; đặt toàn bộ bước tạo record + `EnqueueMatch` trong transaction hoặc rollback/đánh dấu `delete_pending` khi enqueue thất bại.
  - **Kiểm chứng:** Test case `QuickMatch` thất bại do JD rỗng / quota exhausted không để lại record nào trong `resumes`, `stored_files`, `quick_match_jds`.
  - **Trạng thái:** Đã triển khai & pass test (`BE04_BE05_QuickMatchDoesNotLeaveOrphansOnErrorsOrQuotaAndValidatesIdempotency`) — Đã nghiệm thu bởi chủ dự án (21/11/2026).

- [x] **BE-05 (P1)** — **Kiểm tra `JobType` và input hash cho Idempotency của `QuickMatch`**
  - **Vị trí:** `backend/CareerGap.Api/Matching/QuickMatchEndpoints.cs:60-62`
  - **Mô tả vấn đề:** Nếu `AiJob` đã tồn tại theo `(RequestedBy, IdempotencyKey)`, endpoint trả ngay `202 Accepted` trỏ sang `/api/matching/pairs/{previous.Id}` mà không kiểm tra `previous.JobType == "match_pair"` hoặc so khớp `InputHash`.
  - **Yêu cầu tinh chỉnh:** Đối chiếu đồng bộ như `PairMatchingService.Enqueue:60-67`: kiểm tra `JobType == "match_pair"` và hash đầu vào, nếu khác trả `409 Conflict` (`idempotency_conflict`).
  - **Kiểm chứng:** Unit/Integration test tái sử dụng `Idempotency-Key` từ `extract_cv` hoặc payload khác vào `/api/matching/quick-pairs` trả `409`.
  - **Trạng thái:** Đã triển khai & pass test (`BE04_BE05_QuickMatchDoesNotLeaveOrphansOnErrorsOrQuotaAndValidatesIdempotency`) — Đã nghiệm thu bởi chủ dự án (21/11/2026).

- [x] **BE-06 (P1)** — **Dọn file CV ẩn danh (`resume-redacted.docx`) cũ khi đổi hoặc ẩn CV công khai**
  - **Vị trí:** `backend/CareerGap.Api/Files/PublicResumeEndpoints.cs:134-141,158`, `backend/CareerGap.Api/Files/OrphanFileCleanupService.cs:21`
  - **Mô tả vấn đề:** Khi ứng viên công khai phiên bản mới hoặc ẩn CV công khai, `StoredFile` ẩn danh cũ vẫn mang `Status = "ready"`, không bao giờ được `OrphanFileCleanupService` (chỉ quét `pending`/`delete_pending`) dọn dẹp.
  - **Yêu cầu tinh chỉnh:** Đánh dấu `StoredFile` ẩn danh cũ sang `delete_pending` khi không còn bản ghi `CandidatePublicResume` nào tham chiếu (sau transaction commit thành công).
  - **Kiểm chứng:** Test trong `PublicResumeTests.cs` & `OrphanFileCleanupTests.cs` xác nhận file ẩn danh cũ chuyển `delete_pending` và được worker dọn.
  - **Trạng thái:** Đã triển khai & pass test (`BE06_ReplacingOrHidingPublicResumeQueuesOldRedactedFileForCleanup`) — Đã nghiệm thu bởi chủ dự án (21/11/2026).

- [x] **BE-07 (P1)** — **Chống race condition khi tạo `JobDataRevision` #1 trong `JobRewriteService`**
  - **Vị trí:** `backend/CareerGap.Api/Writing/JobRewriteService.cs:46-57`
  - **Mô tả vấn đề:** Khi `job.CurrentVersion.Id` chưa có `JobDataRevision`, `Enqueue` chèn `RevisionNo = 1` không khóa `FOR UPDATE` trên `job_versions` và không xử lý mã lỗi trùng khóa Postgres `23505`, dễ văng `500` nếu chạy song song với `revisions/extract`.
  - **Yêu cầu tinh chỉnh:** Khóa hàng `job_versions` trong transaction ngắn hoặc bắt `PostgresException` `23505` để đọc lại revision #1 vừa tạo.
  - **Kiểm chứng:** Test đồng thời 2 request `rewrite-proposals` / `extract` không trả `500`.
  - **Trạng thái:** Đã triển khai & pass test (`BE07_BE08_ConcurrentJobRewriteRevisionCreationAndHistoryDeleteDoNotThrow500`) — Đã nghiệm thu bởi chủ dự án (21/11/2026).

- [x] **BE-08 (P1)** — **Chống race condition khi gọi đồng thời `DELETE /api/matching/history/{id}`**
  - **Vị trí:** `backend/CareerGap.Api/Matching/AnalysisHistory.cs:53-66`
  - **Mô tả vấn đề:** Kiểm tra `!db.AnalysisHistoryHides.Any(...)` rồi insert không có khóa/bắt `23505`, có thể ném `500 DbUpdateException` khi người dùng double-click.
  - **Yêu cầu tinh chỉnh:** Dùng `ON CONFLICT DO NOTHING` hoặc bắt `23505` trên `AnalysisHistoryHide` để trả `204 NoContent` idempotent.
  - **Kiểm chứng:** Test gọi `DELETE` song song trả thành công ổn định, không `500`.
  - **Trạng thái:** Đã triển khai & pass test (`BE07_BE08_ConcurrentJobRewriteRevisionCreationAndHistoryDeleteDoNotThrow500`) — Đã nghiệm thu bởi chủ dự án (21/11/2026).

- [x] **BE-09 (P2)** — **Chặn port ngoài 80/443 tại các bước redirect của `JdUrlImporter`**
  - **Vị trí:** `backend/CareerGap.Api/Matching/JdUrlImporter.cs:52,82-89`
  - **Mô tả vấn đề:** URL gốc kiểm tra port `80`/`443`, nhưng khi theo `Location` redirect ở dòng 86 chỉ kiểm tra `Scheme` và `UserInfo`, bỏ sót kiểm tra port.
  - **Yêu cầu tinh chỉnh:** Tách helper `ValidateExternalUri(Uri)` kiểm tra đủ scheme `http/https`, không `UserInfo`, port mặc định hoặc `80/443` và `EnsurePublicHostAsync` cho cả URL đầu và mọi redirect hop.
  - **Kiểm chứng:** Unit test mock redirect sang `http://example.com:8080/...` bị từ chối.
  - **Trạng thái:** Đã triển khai & pass test (`BE09_BE10_JdUrlRedirectPortBlockedAndHttpsRecoveryConfigurable`; đã đồng bộ `StageP2Batch4Tests.cs:82` về `http://127.0.0.1/secret`) — Đã nghiệm thu bởi chủ dự án (21/11/2026).

- [x] **BE-10 (P2)** — **Cấu hình hóa domain cho link khôi phục mật khẩu thay vì khóa cứng `http://` loopback**
  - **Vị trí:** `backend/CareerGap.Api/Accounts/AccountEndpoints.cs:153-155`
  - **Mô tả vấn đề:** `ForgotPasswordAsync` kiểm tra `parsedBase.Scheme == "http" && parsedBase.IsLoopback`, sẽ im lặng không gửi email nếu triển khai lên domain HTTPS thật.
  - **Yêu cầu tinh chỉnh:** Đọc danh sách origin/baseUrl hợp lệ từ cấu hình (`Auth:AllowedRecoveryOrigins` / `App:PublicBaseUrl`), mặc định giữ loopback khi không cấu hình thêm.
  - **Kiểm chứng:** Test với config origin HTTPS hợp lệ vẫn gửi email khôi phục, origin lạ bị từ chối.
  - **Trạng thái:** Đã triển khai & pass test (`BE09_BE10_JdUrlRedirectPortBlockedAndHttpsRecoveryConfigurable`) — Đã nghiệm thu bởi chủ dự án (21/11/2026).

- [x] **BE-11 (P2)** — **Chuẩn hóa định dạng phản hồi lỗi toàn API về RFC 7807 `ProblemDetails`**
  - **Vị trí:** `Recruitment/CompanyJobEndpoints.cs`, `Profiles/ProfileEndpoints.cs`, `Files/FileEndpoints.cs`, `Files/ResumeRevisionEndpoints.cs`, `Files/PublicResumeEndpoints.cs`, `Applications/ApplicationEndpoints.cs`, `Applications/ApplicationWorkflowEndpoints.cs`, `Community/CommunityEndpoints.cs`, `Recruitment/BusinessIdentity.cs`, `Learning/SavedRoadmap.cs`
  - **Mô tả vấn đề:** Đang trộn lẫn `Results.Problem(...)`, `Results.Conflict(new { title = "..." })` (thiếu `status`/`type`), và `Results.BadRequest()` rỗng không có mã lỗi.
  - **Yêu cầu tinh chỉnh:** Tạo helper trả lỗi thống nhất (`Results.Problem(statusCode: ..., title: ...)`), loại bỏ mọi `Results.BadRequest()` không tham số và anonymous `{ title }` object.
  - **Kiểm chứng:** Kiểm tra hợp đồng lỗi ở frontend `api.ts` đọc thống nhất `title`/`detail`/`status`; test backend không vỡ contract hiện hành.
  - **Trạng thái:** Đã chuẩn hóa toàn bộ endpoint API (`Results.Problem(statusCode: ...)`), 238/238 backend test pass — Đã nghiệm thu bởi chủ dự án (21/11/2026).

- [x] **BE-12 (P2)** — **Chuẩn hóa envelope phân trang trả về giữa các module**
  - **Vị trí:** `Administration/AdminEndpoints.cs:51,83,115,118`, `Administration/ReportEndpoints.cs:22`, `Recruitment/BusinessIdentity.cs:77`, `Files/FileEndpoints.cs:175`, `Files/PublicResumeEndpoints.cs:185`, `Applications/ApplicationWorkflowEndpoints.cs:141`
  - **Mô tả vấn đề:** Admin endpoints trả mảng trần (`T[]` không có `total`), `/api/admin/reports/inbox` trả `{ items, total, page, pageSize }`, `/api/admin/businesses` trả `{ total, items }`, còn `resumes`/`public/resumes`/`notes` trả mảng trần.
  - **Yêu cầu tinh chỉnh:** Chuẩn hóa về `{ items, total, page, pageSize }` (và giữ trường tương thích hoặc cập nhật đồng bộ `frontend/src/api.ts` cùng lượt).
  - **Kiểm chứng:** Đồng bộ toàn bộ endpoint phân trang + cập nhật các FE unit tests và backend tests liên quan.
  - **Trạng thái:** Đã triển khai & kiểm chứng (26/11/2026) — chuẩn hóa envelope `{ items, total, page, pageSize }` trên `/api/admin/accounts`, `/api/admin/jobs`, `/api/admin/reports`, `/api/admin/audit`, `/api/admin/businesses`, `/api/resumes`, `/api/public/resumes`, `/api/applications/{id}/notes`, đồng bộ `PagedResponse<T>` tại `frontend/src/api.ts` và các màn hình FE; pass **240/240** backend test (bao gồm `BE12_PagedEndpointsReturnStandardEnvelope`), `lint`/`typecheck`/`build` và **47/47** Vitest (run `.local/frontend-step3-20261126-211326-db64`).

- [x] **BE-13 (P3)** — **Đồng bộ quy ước đặt tên bảng/cột DB (`saved_roadmaps` & `community_searchs`)**
  - **Vị trí:** `backend/CareerGap.Api/Data/AppDb.cs:68-74,421`, `backend/CareerGap.Api/Data/CommunityModels.cs`, `backend/CareerGap.Api/Community/CommunityService.cs:111`
  - **Mô tả vấn đề:** Bảng `saved_roadmaps` đang có cột PascalCase (`PairId`, `Plan`, `Completed`, `RowVersion`, `UpdatedAt`) do `SavedRoadmap` nằm ngoài namespace `CareerGap.Api.Data`; `CommunityModels.cs:145` có `.ToTable("community_searches")` bị `AppDb.cs:421` ghi đè thành `community_searchs`.
  - **Yêu cầu tinh chỉnh:** Viết migration đổi tên cột `saved_roadmaps` sang `snake_case` + gọi `Snake(...)` trong `AppDb.cs`; xóa dòng `.ToTable("community_searches")` thừa (hoặc đổi thống nhất qua migration mới).
  - **Kiểm chứng:** Kiểm tra migration Up/Down trên PostgreSQL 16 và `dotnet test`.
  - **Trạng thái:** Đã triển khai & kiểm chứng (26/11/2026) — thêm migration #32 `20261126134034_NormalizeSavedRoadmapsAndCommunitySearches` (`RenameTable`/`RenameColumn` không mất dữ liệu), cập nhật `AppDb.cs`, `CommunityModels.cs`, `CommunityService.cs`, `schema-implementation-status.md`, `PairMigrationTests.cs` (32 migrations); pass **240/240** backend test (bao gồm `PairMigrationTests` và `BE13_SavedRoadmapsColumnsAndCommunitySearchesTableAreNormalized`, run `.local/frontend-step3-20261126-211326-db64`).

- [x] **BE-14 (P3)** — **Sửa `JsonDocument` chưa dispose & đồng nhất kiểm tra quyền `BusinessIdentity` trên Application**
  - **Vị trí:** `backend/CareerGap.Api/Profiles/ProfileEndpoints.cs:121`, `backend/CareerGap.Api/Applications/ApplicationEndpoints.cs:174,196,218` vs `ApplicationWorkflowEndpoints.cs:233-234`
  - **Mô tả vấn đề:** `ToFullDto` gọi `JsonDocument.Parse(profile.Details)` không dùng `using`; `ApplicationEndpoints` đọc hồ sơ chỉ kiểm `Company.OwnerUserId == userId` trong khi `ApplicationWorkflowEndpoints` kiểm tra thêm `BusinessIdentities`.
  - **Yêu cầu tinh chỉnh:** Bọc `using var doc = ...` trong `ToFullDto` và trích helper dùng chung kiểm quyền sở hữu tin cho cả đọc và ghi Application.
  - **Kiểm chứng:** `dotnet test` toàn bộ suite Applications & Profiles đạt.
  - **Trạng thái:** Đã triển khai & pass toàn bộ 238/238 backend test — Đã nghiệm thu bởi chủ dự án (21/11/2026).

---

## Nhóm 2 — Sửa lỗi Logic, Polling, Routing & Luồng chuyển tiếp Frontend (`FE-01` .. `FE-13`)

- [x] **FE-01 (P0)** — **Bổ sung nút "Đối chiếu CV–JD" trực tiếp tại Chi tiết tin tuyển dụng (`#job?id=...`) và mở rộng tìm tin ở `QuickMatchPanel`**
  - **Vị trí:** `frontend/src/jobs.tsx:312-324`, `frontend/src/quickMatch.tsx:39,130-132`
  - **Mô tả vấn đề:** Xem chi tiết tin không có nút chạy `PairAnalysisButton` hoặc mở đối chiếu cho chính tin đó; `QuickMatchPanel` chỉ tải `page=1&pageSize=20` nên không chọn được tin ngoài top 20.
  - **Yêu cầu tinh chỉnh:** Thêm khối "Đối chiếu CV của bạn với tin tuyển dụng này" (`PairAnalysisButton` / link mở `QuickMatchPanel` đã chọn sẵn `jobId`) trên trang chi tiết `#job?id=...`; bổ sung tìm kiếm/tải tin theo từ khóa hoặc nhận `preselectedJob` trong `QuickMatchPanel`.
  - **Kiểm chứng:** Từ `#job?id=...`, ứng viên chọn CV và chạy/xem kết quả đối chiếu ngay mà không phải quay lại tìm thủ công.
  - **Trạng thái:** Đã nghiệm thu (21/11/2026) — `PairAnalysisButton` tại `#job?id=...`, tìm tin theo từ khóa + `preselectedJob` ở `QuickMatchPanel`; pass lint/typecheck/build, Vitest 47/47 & Playwright E2E 8/8 (`group2-fe.spec.ts`).

- [x] **FE-02 (P1)** — **Bổ sung bước chuyển tiếp sau khi xem kết quả Đối chiếu (`PairDetails`) và Lộ trình học (`WeeklyRoadmap`)**
  - **Vị trí:** `frontend/src/pairMatching.tsx:32-51`, `frontend/src/weeklyRoadmap.tsx:8-46`
  - **Mô tả vấn đề:** Xem xong điểm và lỗ hổng kỹ năng không có CTA dẫn sang `#cv-builder` / `#private-resumes` để cập nhật CV hoặc về `#job?id=...` để nộp hồ sơ.
  - **Yêu cầu tinh chỉnh:** Thêm thanh hành động cuối panel `PairDetails` & `WeeklyRoadmap`: `"Mở trình tạo CV để bổ sung"` (`#cv-builder`), `"Quản lý CV của tôi"` (`#private-resumes`) và `"Xem tin tuyển dụng & Ứng tuyển"` (`#job?id=...` nếu là tin trên sàn).
  - **Kiểm chứng:** E2E đi trọn luồng Xem chi tiết tin → Đối chiếu → Lộ trình học → Mở CV Builder / Quay lại Ứng tuyển.
  - **Trạng thái:** Đã nghiệm thu (21/11/2026) — thanh CTA (`#cv-builder`, `#private-resumes`, `#job?id=...`) cuối `PairDetails` & `WeeklyRoadmap`; pass lint/typecheck/build, Vitest 47/47 & Playwright E2E 8/8 (`group2-fe.spec.ts`).

- [x] **FE-03 (P1)** — **Sửa nút "Tìm tin này trong danh sách" (`matching.tsx`) khỏi trả về 0 kết quả**
  - **Vị trí:** `frontend/src/matching.tsx:69`, `frontend/src/jobs.tsx:307-310`
  - **Mô tả vấn đề:** Nút hiện chỉ đặt `filters.q = title` và giữ nguyên các bộ lọc địa điểm/tag/hình thức đang bật, dễ làm mất chính tin vừa chọn.
  - **Yêu cầu tinh chỉnh:** Đổi thành liên kết mở thẳng trang chi tiết `jobHref(item.jobId)` (`"Xem chi tiết tin"`) hoặc reset bộ lọc xung đột khi lọc theo từ khóa.
  - **Kiểm chứng:** Click vào gợi ý luôn mở đúng tin tuyển dụng, không rơi vào màn hình trống.
  - **Trạng thái:** Đã nghiệm thu (21/11/2026) — đổi thành `<a href={jobHref(item.jobId)}>Xem chi tiết tin</a>` trong `matching.tsx` & truyền `jobId` cho `PairAnalysisButton`; pass lint/typecheck/build, Vitest 47/47 & Playwright E2E 8/8.

- [x] **FE-04 (P0)** — **Giới hạn số lần polling & tạm dừng polling khi tab/view bị ẩn dưới `PersistentView`**
  - **Vị trí:** `frontend/src/quickMatch.tsx:50-66`, `frontend/src/pairMatching.tsx:63-75,105-117`, `frontend/src/writing.tsx:18-25`, `frontend/src/jobs.tsx:555-569`, `frontend/src/persistentView.tsx:15-30`
  - **Mô tả vấn đề:** 4 component AI không có trần số lần poll (`maxAttempts`) và vẫn chạy vô hạn khi `PersistentView` giữ component mounted dù người dùng chuyển cửa sổ. `requestJdRewrite` (`jobs.tsx`) dùng vòng lặp `for` không hủy được khi unmount hoặc bấm `"Hủy sửa"`.
  - **Yêu cầu tinh chỉnh:** Tạo hook dùng chung `useAiJobPolling(jobId, { active, maxAttempts: 60 })` tự dừng khi đạt giới hạn hoặc khi view không active (và tự kiểm lại 1 lần khi view active trở lại); thêm `AbortController`/ref guard cho `requestJdRewrite`.
  - **Kiểm chứng:** Unit test xác minh polling dừng khi quá 60 lần hoặc khi chuyển sang route khác; báo thông báo rõ khi timeout.
  - **Trạng thái:** Đã nghiệm thu (21/11/2026) — `useAiJobPolling.ts` + `useViewActive()` cho 4 panel AI & `AbortController` cho `requestJdRewrite`; pass `useAiJobPolling.test.ts` (3/3) & Playwright E2E 8/8 (`group2-fe.spec.ts`).

- [x] **FE-05 (P1)** — **Sửa gọi API 2 lần gây nháy ở `WeeklyRoadmap` và loại bỏ poll kép ở `FullCvReview`**
  - **Vị trí:** `frontend/src/weeklyRoadmap.tsx:13-24`, `frontend/src/main.tsx:240-257,304-311`, `frontend/src/fullCvReview.tsx:31-53`
  - **Mô tả vấn đề:** `WeeklyRoadmap` gọi `setLoaded(true)` ở effect 1 làm kích hoạt ngay effect 2 xóa `plan` và fetch lại `/roadmap/${pairId}` lần nữa. `FullCvReview` và `main.tsx` poll 2 luồng song song khi trích xuất CV đầy đủ.
  - **Yêu cầu tinh chỉnh:** Gộp 2 `useEffect` trong `WeeklyRoadmap` thành 1 luồng tải duy nhất; để `main.tsx` giữ 1 luồng poll chính và `FullCvReview` chỉ refresh một lần khi `job.status` đạt terminal (`succeeded`/`failed`).
  - **Kiểm chứng:** Network log chỉ ghi nhận 1 request khi mở lộ trình và không có effect abort/restart liên tục khi extract CV.
  - **Trạng thái:** Đã nghiệm thu (21/11/2026) — gộp luồng tải `WeeklyRoadmap` & đồng bộ `parentPolling` giữa `main.tsx` với `FullCvReview`; pass lint/typecheck/build, Vitest 47/47 & Playwright E2E 8/8 (`group2-fe.spec.ts`).

- [x] **FE-06 (P1)** — **Chặn role sai vào `#applications`, thống nhất trang mặc định `/` (`#home`) & Logo thương hiệu**
  - **Vị trí:** `frontend/src/navigation.ts:20-39`, `frontend/src/workspace.tsx:39,95,164`, `frontend/src/onboarding.tsx:7-15`
  - **Mô tả vấn đề:** `#applications` chỉ check `session?.authenticated` nên Recruiter/Admin vào sẽ thấy trang trống; mở `/` hoặc click logo `#top` bị fallback về `#jobs` dù mục đầu sidebar là `"Trang chủ tổng hợp"` (`#home`); `"Cách CareerGap hoạt động"` dùng hash `#how-it-works` ăn ké fallback `#jobs`.
  - **Yêu cầu tinh chỉnh:** Gating `#applications` bắt buộc `Candidate` (sai vai trò chuyển về view phù hợp/báo không có quyền); đổi mặc định `/` và link logo về `#home`; tách `"Cách CareerGap hoạt động"` thành `<Dialog>` hướng dẫn dùng chung hoặc route rõ ràng.
  - **Kiểm chứng:** Cập nhật `navigation.test.ts` kiểm tra đủ quyền Guest/Candidate/Recruiter/Admin và default `#home`.
  - **Trạng thái:** Đã nghiệm thu (21/11/2026) — gating `#applications` (`Candidate`), mặc định + logo `#home`, tách `<Dialog>` hướng dẫn; pass `navigation.test.ts` (12/12) & Playwright E2E 8/8.

- [x] **FE-07 (P1)** — **Lưu bộ lọc `#jobs` vào URL hash & sửa lỗi cướp focus bàn phím khi đổi filter**
  - **Vị trí:** `frontend/src/jobs.tsx:149-153,291`, `frontend/src/navigation.ts:53-60`, `frontend/src/analysisHistory.tsx:45-46`
  - **Mô tả vấn đề:** `#jobs` chỉ đồng bộ 4 tham số chiều vào (bỏ sót `skill`, `industry`, `workMode`, `employmentType`, `experienceYears`, `page`) và không ghi ngược ra URL khi submit → bấm Back ghi đè mất bộ lọc. Ngược lại, `useWorkspaceNavigation` chạy `focus()` + `scrollTo(0, 0)` theo cả thay đổi `hash` query string, khiến `AnalysisHistory` bị giật focus khỏi `<select>` mỗi lần chọn lọc.
  - **Yêu cầu tinh chỉnh:** Chỉ chạy `focus()` + `scrollTo(0, 0)` khi **view ID** thực sự thay đổi (không chạy khi chỉ đổi query string trong cùng view); đồng bộ đầy đủ query params cho `#jobs` hai chiều.
  - **Kiểm chứng:** Đổi filter trên `#analysis-history` và `#jobs` giữ nguyên focus bàn phím; bấm Back từ `#job?id=...` khôi phục đúng trang/bộ lọc trước đó.
  - **Trạng thái:** Đã nghiệm thu (21/11/2026) — `useLayoutEffect` phụ thuộc `[view, loading]` & đồng bộ 2 chiều đủ 9 bộ lọc + `page` ở `#jobs` (bảo toàn filter nháp trên `#jobs` dưới `PersistentView`); pass Playwright E2E (`group2-fe.spec.ts`, `workspace.spec.ts`).

- [x] **FE-08 (P1)** — **Hỗ trợ `returnTo` sau đăng nhập cho mọi trang & tự động tải `#shared-profile?profile=...`**
  - **Vị trí:** `frontend/src/jobLinks.ts:25-30`, `frontend/src/main.tsx:136,266-267`, `frontend/src/matching.tsx:102`, `frontend/src/jobs.tsx:736`
  - **Mô tả vấn đề:** Guest bấm link tới `#private-resumes`, `#analysis-history`, `#applications`... bị đẩy về `#access` nhưng sau login luôn rơi về `#jobs`; `#shared-profile` bắt recruiter bấm thêm nút `"Xem hồ sơ"` sau khi mở tab mới.
  - **Yêu cầu tinh chỉnh:** Lưu hash đích vào `returnTo` khi redirect về `#access` và khôi phục sau khi đăng nhập thành công (nếu role hợp lệ); tự động gọi load profile khi `profileId` có sẵn trong URL ở `RecruiterProfileLookup`.
  - **Kiểm chứng:** Đăng nhập từ link được chia sẻ quay lại đúng trang mong muốn; `#shared-profile` tải hồ sơ ngay khi mở.
  - **Trạng thái:** Đã nghiệm thu (21/11/2026) — `signInReturnHref` & `safeReturnAfterSignIn` (pass `jobLinks.test.ts` 4/4), tự tải hồ sơ khi có `profile` ở `#shared-profile` & pass Playwright E2E (`group2-fe.spec.ts`).

- [x] **FE-09 (P1)** — **Chống crash `JSON.parse`, hiện lỗi mạng kèm nút Thử lại & dọn thông báo cũ khi đổi tab Đăng nhập**
  - **Vị trí:** `frontend/src/main.tsx:69-92,262`, `frontend/src/publicHome.tsx:52-53`, `frontend/src/quickMatch.tsx:46`
  - **Mô tả vấn đề:** `JSON.parse(job.outputJson)` không có `try/catch`; `PublicHome` và `QuickMatchPanel` nuốt lỗi fetch thành danh sách rỗng; chuyển qua lại giữa Login/Register/Forgot không xóa `error`/`message` cũ.
  - **Yêu cầu tinh chỉnh:** Bọc `try/catch` cho `JSON.parse`; hiển thị `<Alert tone="error">` + nút `"Thử lại"` khi tải dữ liệu lỗi; reset `error`/`message` mỗi khi đổi `mode` trong `AccountAccess`.
  - **Kiểm chứng:** Unit test cho các trạng thái lỗi tải dữ liệu và chuyển tab auth.
  - **Trạng thái:** Đã nghiệm thu (21/11/2026) — bọc `try/catch` cho `outputJson`, hiển thị lỗi + nút `"Thử lại"` ở `PublicHome` & `QuickMatchPanel`, reset state khi đổi tab ở `AccountAccess`; pass Playwright E2E (`group2-fe.spec.ts`, `accounts.spec.ts`).

- [x] **FE-10 (P2)** — **Tối ưu fetch `AnalysisHistory` và đồng bộ phân trang `ResumeBuilderPanel`**
  - **Vị trí:** `frontend/src/analysisHistory.tsx:29-39`, `frontend/src/main.tsx:200-205`, `frontend/src/resumeBuilder.tsx:218-219`
  - **Mô tả vấn đề:** Khi mở chi tiết `#analysis-history?id=...`, list effect vẫn gọi `/api/matching/history` dù danh sách không hiển thị, trong khi bản chi tiết `queued`/`running` lại không tự poll. Khi lưu CV từ Builder, `main.tsx` cập nhật trang 1 nhưng không đặt lại `page = 1`.
  - **Yêu cầu tinh chỉnh:** Ngừng gọi API list khi `selectedId` đang có giá trị; auto-poll chi tiết nếu `status` là `queued`/`running`; đồng bộ reset `page = 1` khi `onResumesUpdated` chạy.
  - **Kiểm chứng:** Network tab không gọi thừa API list khi mở chi tiết lịch sử; danh sách CV đồng bộ trang 1 sau khi lưu từ Builder.
  - **Trạng thái:** Đã nghiệm thu (21/11/2026) — ngừng fetch list khi có `id`, thêm auto-poll chi tiết `AnalysisHistory`, đặt lại `page = 1` khi `ResumeBuilderPanel` cập nhật danh sách; pass Playwright E2E (`group2-fe.spec.ts`, `cv-builder.spec.ts`).

- [x] **FE-11 (P2)** — **Đăng ký biến thể chữ đậm (`NotoSans-Bold.ttf`) cho trình xuất PDF**
  - **Vị trí:** `frontend/src/resumeBuilderPdf.tsx:22-35`, `frontend/public/fonts/`
  - **Mô tả vấn đề:** Chỉ đăng ký `NotoSans-Regular.ttf`, trong khi `styles.name`, `styles.heading`, `styles.itemTitle` dùng `fontWeight: 700` → chữ tiêu đề không thực sự in đậm trong file PDF xuất ra.
  - **Yêu cầu tinh chỉnh:** Bổ sung `NotoSans-Bold.ttf` (cùng bộ OFL) vào `frontend/public/fonts/` và đăng ký `{ src: '/fonts/NotoSans-Bold.ttf', fontWeight: 700 }` trong `Font.register`.
  - **Kiểm chứng:** Xuất PDF tiếng Việt/Anh và kiểm tra trực quan + text selection/copy giữ nguyên Unicode.
  - **Trạng thái:** Đã nghiệm thu (21/11/2026) — đã thêm `NotoSans-Bold.ttf` (631.484 bytes, OFL) & đăng ký đủ weight `400`/`700` trong `resumeBuilderPdf.tsx`; pass Playwright E2E (`group2-fe.spec.ts`, `cv-builder.spec.ts`).

- [x] **FE-12 (P2)** — **Hoàn thiện tính nhất quán form Hồ sơ, Đăng ký & cảnh báo mất nháp CV Builder**
  - **Vị trí:** `frontend/src/main.tsx:76-77,116-129`, `frontend/src/resumeBuilder.tsx:134-143`
  - **Mô tả vấn đề:** `CandidateProfilePanel` gửi `preferredWorkMode` lên API nhưng không có ô nhập trên UI; form Đăng ký giữ nhãn `"Tên hiển thị (ứng viên)"` kể cả khi chọn `"Nhà tuyển dụng"`; `ResumeBuilderPanel` khi đổi target CV (`changeTarget`) xóa sạch nháp đang sửa mà không hỏi xác nhận.
  - **Yêu cầu tinh chỉnh:** Thêm `<select>` hình thức làm việc mong muốn vào `CandidateProfilePanel`; ẩn hoặc đổi nhãn phù hợp khi chọn vai trò Nhà tuyển dụng; bật `ConfirmDialog` khi `dirty` mà người dùng đổi target hoặc rời form CV Builder.
  - **Kiểm chứng:** Lưu và đọc lại `preferredWorkMode`; test bảo vệ nháp chưa lưu khi đổi mục tiêu CV.
  - **Trạng thái:** Đã nghiệm thu (21/11/2026) — `<select>` `preferredWorkMode` + checkbox chia sẻ trong `CandidateProfilePanel`, ẩn ô tên ứng viên khi đăng ký `Recruiter`, `<Dialog>` xác nhận mất nháp khi đổi target CV; pass Playwright E2E (`group2-fe.spec.ts`).

- [x] **FE-13 (P2)** — **Bổ sung `type="button"` cho toàn bộ nút không phải submit form**
  - **Vị trí:** Rà soát toàn bộ `frontend/src/*.tsx` (`main.tsx`, `jobs.tsx`, `matching.tsx`, `pairMatching.tsx`, `weeklyRoadmap.tsx`, `analysisHistory.tsx`, `fullCvReview.tsx`, `resumeBuilder.tsx`, `community.tsx`, `administration.tsx`)
  - **Mô tả vấn đề:** Nhiều `<button>` nằm trong `<form>` hoặc gần form không khai báo `type="button"`, mặc định trình duyệt hiểu là `submit`.
  - **Yêu cầu tinh chỉnh:** Thêm `type="button"` tường minh cho mọi nút điều hướng/thao tác phụ, chỉ giữ `type="submit"` cho nút gửi form chính.
  - **Kiểm chứng:** Bấm các nút thao tác phụ bên trong form không gây submit form ngoài ý muốn.
  - **Trạng thái:** Đã nghiệm thu (21/11/2026) — bổ sung `type="button"` / `type="submit"` tường minh trên toàn bộ `frontend/src/*.tsx` (`0` nút thiếu `type`); pass lint/typecheck/build & Playwright E2E 8/8.

---

## Nhóm 3 — Đồng bộ Design System & CSS toàn trang web (`UI-01` .. `UI-08`)

- [x] **UI-01 (P1)** — **Gom kiến trúc CSS layers giữa `style.css` và `community.css`, gỡ bỏ `!important`**
  - **Vị trí:** `frontend/src/style.css:123-598`, `frontend/src/community.css:1-22`
  - **Mô tả vấn đề:** `style.css` bị xé làm 3 khối `@layer components` + 1 khối không nằm trong layer (dòng `571–598`), còn `community.css` hoàn toàn unlayered và lạm dụng `!important` (`.social-post { padding: ... !important }`, `.social-body-editor { min-height: 300px !important }`), khiến utility của Tailwind v4 bị ghi đè tùy tiện.
  - **Yêu cầu tinh chỉnh:** Đưa toàn bộ class giao diện tùy chỉnh (kể cả phần mở rộng và `community.css`) vào đúng một tầng `@layer components` chuẩn, xóa các rule trùng lặp (`.social-live-status` lặp 3 lần trong `community.css`) và loại bỏ `!important`.
  - **Kiểm chứng:** `npm run build` sạch sẽ, kiểm tra trực quan mọi view không bị vỡ layout do ưu tiên CSS.
  - **Trạng thái:** Đã nghiệm thu (21/11/2026) — chuẩn hóa thứ tự import CSS tại `main.tsx` trước `community.css`, hợp nhất `style.css` & `community.css` vào `@layer components`, gộp `.social-live-status`/`.social-count`, gỡ toàn bộ `!important`; pass kiểm tra tĩnh CSS (`0` `!important`), bundle `dist/`, Vitest 47/47, Playwright E2E 8/8 & computed style trên `#community`.

- [x] **UI-02 (P1)** *(Đã hoàn thành 27/11/2026)* — **Sửa biến CSS chưa định nghĩa & thay thế ~75 mã hex cứng bằng Design Tokens**
  - **Vị trí:** `frontend/src/style.css:582` (`--border`), `frontend/src/community.css:18-19` (`--surface-alt`, 32 mã hex), `frontend/src/workspace.tsx` (Tailwind arbitrary hex `bg-[#245c48]`, `text-[#303b34]`...)
  - **Mô tả vấn đề:** `--border` và `--surface-alt` chưa từng được khai báo ở `:root`, làm block code cộng đồng fallback sang xanh xám lạnh `#f1f5f9` lạc tông với nền sage ấm `#f4f6f1`. Khoảng 43 mã hex ở `style.css` và 32 mã hex ở `community.css` đi vòng qua hệ token `:root`.
  - **Yêu cầu tinh chỉnh:** Bổ sung đầy đủ semantic tokens trong `:root` (bảng màu Warm Sand / Pine Forest / Neutral Border / AI Accent / Status / Code surface) và thay toàn bộ biến sai + hex cứng ở `style.css`, `community.css`, `workspace.tsx`.
  - **Kiểm chứng:** Quét regex không còn hex cứng ngoài `:root` (ngoại trừ `resumeBuilderPdf.tsx` vốn dùng stylesheet riêng của `@react-pdf/renderer`, đồng bộ bảng màu tương ứng).

- [x] **UI-03 (P1)** *(Đã hoàn thành 27/11/2026)* — **Sửa lỗi media query trùng lặp làm chữ bảng Admin mobile to hơn desktop**
  - **Vị trí:** `frontend/src/style.css:404` vs `frontend/src/style.css:561`
  - **Mô tả vấn đề:** Hai khối `@media (max-width: 639px)` đặt nối tiếp cho `.admin-panel th, .admin-panel td`: dòng 404 đặt `font-size: var(--font-size-sm)` (`12px`), nhưng dòng 561 ghi đè thành `var(--font-size-md)` (`14px`) to hơn cả desktop (`12px` ở dòng 342).
  - **Yêu cầu tinh chỉnh:** Xóa rule trùng, giữ 1 quy tắc responsive chuẩn cho bảng Admin kèm wrapper `.table-scroll` (`overflow-x: auto`) để bảng không bị nén chữ trên màn 320–375px.
  - **Kiểm chứng:** Chụp/kiểm thử responsive 375px của tab Quản trị không tràn ngang và cỡ chữ đồng bộ.

- [x] **UI-04 (P1)** *(Đã hoàn thành 27/11/2026)* — **Đồng bộ hệ thống Typography, Bo góc, Shadow, Focus Ring & Breakpoints trên toàn ứng dụng**
  - **Vị trí:** `frontend/src/style.css`, `frontend/src/community.css`
  - **Mô tả vấn đề:** App chính dùng hệ `px` (`13px` body, bo góc `8px`/`6px`, card phẳng không shadow, focus ring `2px #397558`, breakpoints `1440/1279/1023/639`), các phần thêm sau dùng bo góc `12px` (`.weekly-roadmap`, `.skill-picker`), còn Cộng đồng dùng `rem` (`14.4px` body, H1 lên tới `42.4px`, card có `box-shadow`, focus ring `3px #72a68a`, breakpoints `1500/1250/700`).
  - **Yêu cầu tinh chỉnh:** Thống nhất một Design System duy nhất cho cả App chính & Cộng đồng: nâng cỡ chữ body/input lên `14px–15px` (theo tinh thần đề xuất `ui-unification-2026-11-01.md`), chuẩn hóa radius (`8px` card, `6px` input/button, `999px` pill), dùng `--shadow-sm` chỉ cho thành phần nổi/sticky, 1 focus ring (`2px solid var(--color-focus)`), và 1 bộ breakpoint (`1280px / 1024px / 640px`).
  - **Kiểm chứng:** Chuyển qua lại giữa `#home`, `#jobs`, `#cv-builder`, `#community`, `#admin` giữ nguyên nhịp chữ, bo góc và độ cao nút.

- [x] **UI-05 (P1)** *(Đã hoàn thành 27/11/2026)* — **Sửa lỗi lệch cột `.form-columns` + `.card + .card` & lỗi giãn dòng chip/tag**
  - **Vị trí:** `frontend/src/style.css:155,179`, `frontend/src/publicHome.tsx:81,85,98,118,131`, `frontend/src/administration.tsx:90-99`, `frontend/src/community.tsx:96`
  - **Mô tả vấn đề:** `PublicHome` và `AdminPanel` đặt các `<article className="card">` vào trong `.form-columns`, kích hoạt rule `.card + .card { margin-top: 28px; }` khiến **cột bên phải bị tụt xuống 28px so với cột bên trái**; `PublicHome` còn nhét 3 thẻ hướng dẫn vào lưới 2 cột (lẻ 1 thẻ). Các hàng pill (`Tag phổ biến`, `Địa điểm`, `.social-trending-searches`) dùng `.row` (`justify-content: space-between`) làm nút bị kéo giãn rời rạc.
  - **Yêu cầu tinh chỉnh:** Bổ sung class lưới chuyên dụng `.card-grid-2` / `.card-grid-3` (reset `margin-top: 0` cho con trực tiếp, đặt `gap: 20px`) và `.chip-row` (`display: flex; flex-wrap: wrap; gap: 8px; justify-content: flex-start;`) thay thế ở mọi nơi hiển thị tag/địa điểm/từ khóa xu hướng.
  - **Kiểm chứng:** Kiểm tra trên desktop (1280px+) các card trong một hàng ngang bằng đỉnh trên tuyệt đối, 3 bước hướng dẫn trên `#home` nằm trên 1 hàng 3 cột.

- [x] **UI-06 (P2)** *(Đã hoàn thành 27/11/2026)* — **Thống nhất kích thước Logo công ty & Avatar người dùng**
  - **Vị trí:** `frontend/src/style.css:195,587`, `frontend/src/companyLogo.tsx`, `frontend/src/communityCareer.tsx:9`, `frontend/src/community.css`
  - **Mô tả vấn đề:** Đang tồn tại 3 kích thước/bo góc khác nhau không rõ chuẩn (`CompanyLogo` `52×52px` radius `10px`, `.company-mark` `42×42px` radius `6px`, `.social-avatar` `38×38px` tròn).
  - **Yêu cầu tinh chỉnh:** Định nghĩa token kích thước avatar/logo (`sm: 32px`, `md: 40px`, `lg: 52px`) và chuẩn bo góc phân biệt rõ Avatar cá nhân (tròn) vs Logo doanh nghiệp (bo góc `8px` có viền nhạt).
  - **Kiểm chứng:** Logo trên `#home`, `#jobs`, `#recruiter-company` và thẻ nghề nghiệp `#community` đồng đều.

- [x] **UI-07 (P2)** *(Đã hoàn thành 27/11/2026)* — **Áp dụng `useStickyFit` cho sidebar Cộng đồng & sửa lỗi breakpoint cột CV Builder**
  - **Vị trí:** `frontend/src/community.css:1`, `frontend/src/useStickyFit.ts`, `frontend/src/style.css:474-516`
  - **Mô tả vấn đề:** `.social-sidebar` đặt `position: sticky; top: 24px` không qua `useStickyFit`, dễ bị cắt nội dung phía dưới trên màn hình laptop thấp (768px). Ở CV Builder, `.builder-editing .form-columns` lại nhảy thành 2 cột ở `<= 1023px` trong khi ở `1024px–1439px` chỉ có 1 cột.
  - **Yêu cầu tinh chỉnh:** Gắn `useStickyFit` cho sidebar Cộng đồng và sửa lại thứ tự media query của `.builder-editing .form-columns` cho nhất quán từ desktop → tablet → mobile.
  - **Kiểm chứng:** Kiểm thử ở viewport `1280×720`, `1024×768`, `768×1024`, `375×812`.

- [x] **UI-08 (P2)** *(Đã hoàn thành 27/11/2026)* — **Chuẩn hóa HTML ngữ nghĩa cho `CommunityMarkdown` & sửa rung nút tương tác Cộng đồng**
  - **Vị trí:** `frontend/src/communityMarkdown.tsx:57-62`, `frontend/src/community.tsx:121`, `frontend/src/community.css:1`
  - **Mô tả vấn đề:** Danh sách không thứ tự trong Markdown đang render `<p className="community-bullet">` thay vì `<ul><li>` (dù `community.css` có style `.community-markdown ul`). Nút Vote/Bookmark khi chuyển `aria-pressed="true"` thay đổi border/padding từ `4px 0` sang pill có padding ngang làm xô lệch các nút bên cạnh.
  - **Yêu cầu tinh chỉnh:** Render `<ul><li>` và `<ol><li>` chuẩn cho Markdown; đặt sẵn padding/border trong suốt cố định cho nút `.social-actions .link-button` để chuyển trạng thái không bị giật layout.
  - **Kiểm chứng:** Cập nhật `communityMarkdown.test.tsx`; toggle Vote/Bookmark mượt, không rung chữ.

---

## Nhóm 4 — Đồng bộ Component & Trải nghiệm ở TẤT CẢ cửa sổ (`SYNC-01` .. `SYNC-14`)

- [x] **SYNC-01 (P1)** *(Đã hoàn thành 27/11/2026)* — **Chuẩn hóa `PageHeader` duy nhất cho toàn bộ 15+ cửa sổ & gỡ tiêu đề trùng lặp**
  - **Vị trí:** `frontend/src/workspace.tsx:290-304`, `frontend/src/publicHome.tsx:74`, `frontend/src/jobs.tsx:288,670`, `frontend/src/publicResumes.tsx:63`, `frontend/src/community.tsx:82`, `frontend/src/administration.tsx:82`, `frontend/src/analysisHistory.tsx:40`, `frontend/src/recruitmentNotifications.tsx:59`, `frontend/src/main.tsx:65,141,358`
  - **Mô tả vấn đề:** Hiện 4 view lấy `<h1>` từ `workspace.tsx`, trong khi 8+ view còn lại tự render `<h1>` bên trong `.card` với cỡ chữ và khoảng cách khác nhau; riêng 3 tab Recruiter lặp cùng lúc 2 heading tĩnh (`Tài khoản nhà tuyển dụng` + `Công ty và tin tuyển dụng`) ở phía trên nội dung tab. Mục `#applications` vẫn mang tên cũ `"Đơn ứng tuyển & Thông báo"`.
  - **Yêu cầu tinh chỉnh:** Xây dựng `<PageHeader eyebrow title description actions />` dùng chung ở đầu vùng nội dung chính cho **tất cả** cửa sổ; bỏ 2 khối tiêu đề tĩnh dư thừa của Recruiter; đổi tên `#applications` thành `"Đơn ứng tuyển của tôi"`.
  - **Kiểm chứng:** Mọi cửa sổ đều chỉ có đúng 1 `<h1>` thống nhất kích thước, vị trí và ngữ cấp heading (`h1` → `h2` → `h3`).

- [x] **SYNC-02 (P1)** *(Đã hoàn thành 27/11/2026)* — **Xử lý tình trạng 2 sidebar song song ở cửa sổ Cộng đồng (`#community`)**
  - **Vị trí:** `frontend/src/workspace.tsx`, `frontend/src/community.tsx`, `frontend/src/community.css`, `frontend/src/communityTagFilter.tsx:9-10`
  - **Mô tả vấn đề:** Trên desktop, người dùng mở Cộng đồng thấy cùng lúc `.workspace-sidebar` (232px) bên cạnh `.social-sidebar` (175px) + 3 thanh lọc xếp chồng phía trên feed (`.social-filters` + `.social-trending-searches` + `CommunityTagFilter`), thu hẹp diện tích đọc bài và sụp đổ ở 2 breakpoint khác nhau (`1023px` vs `700px`).
  - **Yêu cầu tinh chỉnh:** Chuyển điều hướng con của Cộng đồng (Bảng tin / Đã lưu / Theo dõi / Bài viết của tôi / Thông báo / Hồ sơ / Kiểm duyệt) thành thanh sub-tabs ngang thống nhất theo mẫu sub-nav của Trung tâm CV & Tuyển dụng; gộp thanh tìm kiếm + lọc tag + chip từ khóa xu hướng vào 1 khối lọc gọn.
  - **Kiểm chứng:** Cửa sổ `#community` sử dụng đúng khung `WorkspaceShell` 1 sidebar chung như mọi cửa sổ khác; bảng tin rộng rãi trên laptop 1280px.

- [x] **SYNC-03 (P1)** *(Đã hoàn thành 27/11/2026)* — **Đồng bộ `ConfirmDialog` cho mọi thao tác xóa, ẩn, khóa, duyệt trên toàn web**
  - **Vị trí:** `frontend/src/ui.tsx:5-53`, `frontend/src/community.tsx:122,167`, `frontend/src/analysisHistory.tsx:15`, `frontend/src/jobs.tsx:572`, `frontend/src/administration.tsx:81,86,103`, `frontend/src/businessIdentity.tsx:27`, `frontend/src/resumeBuilder.tsx:287,294,300`, `frontend/src/publicResumes.tsx:42`
  - **Mô tả vấn đề:** Có nơi dùng `<Dialog>` (`main.tsx`, `reportModeration.tsx`, `communityReports.tsx`), có nơi bật `window.confirm()` gốc trình duyệt (`community.tsx`, `analysisHistory.tsx`, `jobs.tsx`), có nơi bấm 1 phát mất/khóa ngay không hỏi (`administration.tsx`, `businessIdentity.tsx`, `resumeBuilder.tsx`, `publicResumes.tsx`). Ở Admin và BusinessAdmin, ô `"Lý do xử lý"` nằm tách tuốt trên đầu trang làm nút ở dưới bị vô hiệu hóa khó hiểu.
  - **Yêu cầu tinh chỉnh:** Tạo `<ConfirmDialog>` dùng chung (mở rộng từ `<Dialog>`, hỗ trợ tùy chọn ô nhập lý do bắt buộc ngay bên trong modal cho thao tác Admin/BusinessAdmin); thay thế toàn bộ `window.confirm()` và thêm xác nhận cho các hành động hủy/khóa/xóa mục/ẩn CV còn thiếu.
  - **Kiểm chứng:** Không còn `confirm(` gốc trình duyệt trong `frontend/src/`; Admin/BusinessAdmin nhập lý do trực tiếp trong modal khi khóa tài khoản/lưu trữ tin/duyệt doanh nghiệp.

- [x] **SYNC-04 (P1)** *(Đã hoàn thành 27/11/2026)* — **Đồng bộ component `Pagination` duy nhất trên mọi danh sách**
  - **Vị trí:** `frontend/src/businessIdentity.tsx:27`, `frontend/src/recruitmentNotifications.tsx:53,69,78`, `frontend/src/publicResumes.tsx:74`, `frontend/src/main.tsx:315`, `frontend/src/community.tsx:112`, `frontend/src/reportModeration.tsx:53`, `frontend/src/communityReports.tsx:25`, `frontend/src/jobs.tsx:283,513,617`, `frontend/src/analysisHistory.tsx:51`
  - **Mô tả vấn đề:** `BusinessAdmin` thiếu `className="secondary"` nên nút phân trang thành màu xanh primary nổi bật; `RecruitmentNotifications` hiện 2 thanh phân trang trên/dưới kể cả khi có 0 mục; mỗi trang đặt tên nút một kiểu (`Trước/Sau`, `Trang trước`, `Trang thông báo trước`, `Trang doanh nghiệp trước`).
  - **Yêu cầu tinh chỉnh:** Tạo component `<Pagination page totalPages totalItems onPageChange />` đặt thống nhất dưới danh sách, tự ẩn khi `totalPages <= 1`, dùng nút `.secondary`, nhãn thống nhất `"Trang trước"` / `"Trang {page} / {totalPages} ({totalItems} mục)"` / `"Trang sau"`.
  - **Kiểm chứng:** Toàn bộ danh sách dùng chung `<Pagination>` và đồng nhất vị trí/màu sắc.

- [x] **SYNC-05 (P1)** *(Đã hoàn thành 27/11/2026)* — **Đồng bộ `<Alert>`, `<EmptyState>` và `<StaticLoading>` ở tất cả các cửa sổ**
  - **Vị trí:** `frontend/src/pairMatching.tsx:99,134`, `frontend/src/fullCvReview.tsx:54,74`, `frontend/src/writing.tsx:53`, `frontend/src/learning.tsx:13`, `frontend/src/weeklyRoadmap.tsx:40`, `frontend/src/analysisHistory.tsx:42,47`, `frontend/src/jobs.tsx:704`, `frontend/src/recruitmentNotifications.tsx:101`, `frontend/src/community.tsx:104,134,169-175`, `frontend/src/publicResumes.tsx:47-75`, `frontend/src/businessIdentity.tsx:12-27`, `frontend/src/publicHome.tsx:96`, `frontend/src/main.tsx:114`
  - **Mô tả vấn đề:** Hàng loạt `<p role="alert">` và `<div role="alert">` thiếu class `.error` nên không có khung/màu báo lỗi; `PublicResumes`, `BusinessIdentity`, `BusinessAdmin`, `CommunityNotifications` thiếu trạng thái loading (làm nháy dòng "Chưa có dữ liệu" trước khi API trả về) và thiếu `.empty-state`; `Community` đặt thông báo đang tải ở tít dưới cùng ngoài tầm mắt.
  - **Yêu cầu tinh chỉnh:** Tạo primitive `<Alert tone="error|warning|info|success" />` và `<EmptyState icon title description action />`; thay thế toàn bộ alert trần, `<p>` rỗng và bổ sung `<StaticLoading>` nhất quán.
  - **Kiểm chứng:** Mọi màn hình khi loading đều hiện skeleton/trạng thái tải trước khi hiện empty state; mọi lỗi đều hiển thị khối `.error` rõ ràng.

- [x] **SYNC-06 (P1)** *(Đã hoàn thành 27/11/2026)* — **Đồng bộ Từ điển Nhãn trạng thái & Enum tiếng Việt toàn hệ thống**
  - **Vị trí:** `frontend/src/pairMatching.tsx:13,44`, `frontend/src/weeklyRoadmap.tsx:7`, `frontend/src/jobs.tsx:45`, `frontend/src/analysisHistory.tsx`, `frontend/src/administration.tsx`, `frontend/src/ui.tsx`
  - **Mô tả vấn đề:** Cùng một enum hiển thị 2 cách khác nhau giữa các cửa sổ (`career_start`: `"Bắt đầu nghề (Career Start)"` vs `"Bắt đầu nghề"`; `importance`: `"Tùy chọn"`/`"Chưa xác định"` vs `"Ưu tiên"`/`"Chưa phân loại"`). Màu badge trạng thái chưa thống nhất qua bảng tra cứu chung.
  - **Yêu cầu tinh chỉnh:** Tạo module `frontend/src/statusDictionary.ts` chuẩn hóa nhãn tiếng Việt + tone badge cho: Hồ sơ/CV, Dữ liệu trích xuất (`AI đề xuất` vs `Đã xác nhận`), Đơn ứng tuyển, Tin tuyển dụng, Báo cáo, Doanh nghiệp, `careerContext`, `importance`.
  - **Kiểm chứng:** Không còn bất kỳ nhãn enum trùng lặp/lệch tên giữa `#jobs`, `#matching`, `#weekly-roadmap`, `#analysis-history`, `#admin`.

- [x] **SYNC-07 (P1)** *(Đã hoàn thành 27/11/2026)* — **Tinh gọn thẻ tin tuyển dụng ở `#jobs` & cải thiện điểm vào `QuickMatch`**
  - **Vị trí:** `frontend/src/jobs.tsx:81-113,287-310`, `frontend/src/publicHome.tsx:99-109,123`, `frontend/src/quickMatch.tsx:10`
  - **Mô tả vấn đề:** Mỗi thẻ tin ở trang tìm kiếm `#jobs` bung sẵn cả `<select>` CV, `<textarea>` Cover Letter 5 dòng và nút `"Nộp hồ sơ"`, làm danh sách quá dài và rối mắt (khác hẳn thẻ gọn ở `#home`). Nút `"Thử đối chiếu CV–JD"` ở `#home` dẫn sang `#jobs` nhưng `QuickMatchPanel` lại mặc định đóng.
  - **Yêu cầu tinh chỉnh:** Rút gọn thẻ tin ở `#jobs` thành thẻ tóm tắt đồng bộ với `#home` (tiêu đề, công ty/logo, lương, địa điểm, tag, trạng thái đã lưu/ứng tuyển, 2 nút `"Xem chi tiết"` / `"Ứng tuyển"` mở `#job?id=...` hoặc modal ứng tuyển nhanh); nếu vào `#jobs?quickMatch=1` từ `#home`, tự động mở rộng `QuickMatchPanel`.
  - **Kiểm chứng:** Danh sách `#jobs` gọn gàng, thao tác ứng tuyển ở `#job?id=...` hoạt động mượt mà với đầy đủ tính năng Cover Letter/idempotency.

- [x] **SYNC-08 (P1)** *(Đã hoàn thành 27/11/2026)* — **Tinh gọn trang Quản lý tin của Nhà tuyển dụng (`#recruiter-jobs`)**
  - **Vị trí:** `frontend/src/jobs.tsx:685-748`
  - **Mô tả vấn đề:** Form `"Tạo tin nháp"` hơn 15 trường luôn mở sẵn đè lên đầu trang đẩy danh sách tin xuống rất sâu; mỗi thẻ tin lại bung toàn bộ bảng duyệt yêu cầu JD (`JobRequirementReview`) và công cụ tìm CV (`RecruiterKeywordResumeSearch`) ngay trong danh sách.
  - **Yêu cầu tinh chỉnh:** Đặt nút chính `"+ Tạo tin tuyển dụng mới"` ở đầu danh sách (mở drawer/modal hoặc form thu gọn có nút đóng); gom `"Rà soát yêu cầu JD"`, `"Tìm ứng viên phù hợp"`, `"AI viết lại JD"` vào các vùng/tab thu gọn theo từng tin.
  - **Kiểm chứng:** Nhà tuyển dụng vào `#recruiter-jobs` thấy ngay danh sách tin của mình và bộ lọc trạng thái trong 1 màn hình đầu tiên.

- [x] **SYNC-09 (P2)** *(Đã hoàn thành 27/11/2026)* — **Đồng bộ giao diện Trung tâm Thông báo Tuyển dụng và Thông báo Cộng đồng**
  - **Vị trí:** `frontend/src/recruitmentNotifications.tsx`, `frontend/src/community.tsx:169-175`
  - **Mô tả vấn đề:** Hai trang thông báo dùng 2 cách hiển thị khác nhau: Tuyển dụng dùng `.notification-unread` viền trái, 2 bộ lọc select, nút `"Đọc tất cả thông báo"` / `"Đánh dấu đã đọc"`, icon `Bell`; Cộng đồng dùng `.social-notice.is-read` mờ dần, bộ lọc checkbox, nút `"Đánh dấu tất cả đã đọc"` / `"Đã đọc"`, không icon/loading.
  - **Yêu cầu tinh chỉnh:** Thống nhất một mẫu thẻ thông báo (viền trái cho mục chưa đọc, nhãn `"Đánh dấu đã đọc"` & `"Đánh dấu tất cả đã đọc"`, trạng thái kết nối SSE, bộ lọc select `"Tất cả / Chưa đọc"` và empty/loading state đồng bộ).
  - **Kiểm chứng:** Chuyển qua lại giữa thông báo Tuyển dụng và Cộng đồng có trải nghiệm nhất quán.

- [x] **SYNC-10 (P2)** *(Đã hoàn thành 27/11/2026)* — **Đồng bộ mô hình Kiểm duyệt Báo cáo Tuyển dụng & Cộng đồng**
  - **Vị trí:** `frontend/src/reportModeration.tsx`, `frontend/src/communityReports.tsx`
  - **Mô tả vấn đề:** Báo cáo tuyển dụng dùng state cục bộ, mở chi tiết ở bên dưới danh sách và dùng `pageSize = 10`; báo cáo cộng đồng dùng hash URL (`?report=...`), thay thế danh sách bằng màn chi tiết có nút `"Quay lại"`, và dùng `pageSize = 20`.
  - **Yêu cầu tinh chỉnh:** Chuẩn hóa 1 mẫu master-detail thống nhất (dùng hash query `?report=...` hoặc drawer chi tiết đồng bộ, `pageSize` thống nhất, cùng bộ lọc trạng thái và nút thao tác trong `ConfirmDialog`).
  - **Kiểm chứng:** Admin thao tác kiểm duyệt cả 2 loại báo cáo theo cùng quy trình.

- [x] **SYNC-11 (P2)** *(Đã hoàn thành 27/11/2026)* — **Đồng bộ bộ điều khiển Form (`LocationInput`, `SkillPicker`, Ngôn ngữ PDF, Checkbox)**
  - **Vị trí:** `frontend/src/locationInput.tsx`, `frontend/src/skillPicker.tsx`, `frontend/src/main.tsx:126-128`, `frontend/src/resumeBuilder.tsx:265,277,282-283`, `frontend/src/writing.tsx:47`, `frontend/src/community.tsx:143,169-175`
  - **Mô tả vấn đề:** `LocationInput` mới dùng ở tìm việc, chưa dùng ở Hồ sơ ứng viên và CV Builder; nhập kỹ năng ở Hồ sơ dùng dấu phẩy trong `<input>`, ở Builder lại hiện cả `<textarea>` lẫn `SkillPicker` không có nút gỡ chip; chọn ngôn ngữ PDF bị lặp 2 lần giữa `resumeBuilder.tsx` và `writing.tsx`; checkbox ở Cộng đồng dùng `.row` đẩy chữ ra xa ô tick thay vì `.check-option`.
  - **Yêu cầu tinh chỉnh:** Áp dụng `LocationInput` và một `SkillTagInput` (chip có nút `×` xóa + gợi ý từ `SkillCatalog`) thống nhất cho cả Hồ sơ & CV Builder; giữ 1 bộ chọn ngôn ngữ PDF duy nhất trên thanh đầu của CV Builder; chuẩn hóa mọi checkbox về `.check-option`.
  - **Kiểm chứng:** Nhập địa điểm/kỹ năng ở mọi form cho trải nghiệm giống nhau; checkbox căn trái gọn gàng.

- [x] **SYNC-12 (P2)** *(Đã hoàn thành 27/11/2026)* — **Đơn giản hóa thao tác Đọc/Xác nhận CV trong `#private-resumes` & `FullCvReview`**
  - **Vị trí:** `frontend/src/main.tsx:296-311`, `frontend/src/fullCvReview.tsx:20-21,68`
  - **Mô tả vấn đề:** Mỗi phiên bản CV có 2 nút `"Thử trích xuất vX"` (schema v1) và `"Đọc đầy đủ vX"` (schema v3) gây khó hiểu cho người dùng; trong `FullCvReview`, dropdown `"Trường cần bổ sung"` xổ ra danh sách phẳng **166 dòng**.
  - **Yêu cầu tinh chỉnh:** Ẩn/gom `"Thử trích xuất v1"` vào menu nâng cao hoặc giải thích rõ nút chính `"Phân tích nội dung CV"` (v3); gom 166 trường trong `FullCvReview` thành `<optgroup>` theo nhóm (Thông tin chung, Kỹ năng, Kinh nghiệm, Dự án, Học vấn, Chứng chỉ) hoặc chọn nhóm trước rồi chọn mục.
  - **Kiểm chứng:** Người dùng mới dễ dàng nhận biết cần bấm nút nào sau khi tải CV lên.

- [x] **SYNC-13 (P1)** *(Đã hoàn thành 27/11/2026)* — **Gỡ/ẩn giao diện Debug & nhãn "Thử nghiệm / Localhost" khỏi trải nghiệm người dùng**
  - **Vị trí:** `frontend/src/pdfProbe.tsx`, `frontend/src/main.tsx:65,80-83,277,316`, `frontend/src/workspace.tsx:212`, `frontend/src/resumeBuilder.tsx:71,257,327`, `frontend/src/learning.tsx:12`, `frontend/src/businessIdentity.tsx:17`, `frontend/src/administration.tsx:104`
  - **Mô tả vấn đề:** Card QA `"Kiểm tra PDF tiếng Việt — Xuất tài liệu giả"` (`PdfProbe`) hiển thị trực tiếp trong CV Builder của mọi ứng viên; footer ghi `"Bản thử nghiệm · Localhost"`; màn đăng nhập ghi `"Đăng nhập thử nghiệm"`; thông báo hộp thư giả local hiện câu chữ ngay cả khi `localMailbox = false`.
  - **Yêu cầu tinh chỉnh:** Chỉ hiển thị `PdfProbe` và hướng dẫn hộp thư giả khi cờ môi trường dev/test (`import.meta.env.DEV` / flag từ API `/api/auth/recovery-options`) bật; đổi nhãn giao diện thành sản phẩm hoàn chỉnh (`"Đăng nhập"`, `"CareerGap AI · Đồ án Web Tuyển Dụng"`, `"Tạo CV theo mẫu"`).
  - **Kiểm chứng:** Build production (`npm run build`) không hiển thị nút `"Xuất PDF mẫu giả"` hoặc dòng chữ `"Đăng nhập thử nghiệm"` trên giao diện thường.

- [x] **SYNC-14 (P1)** *(Đã hoàn thành 27/11/2026)* — **Dọn sạch thuật ngữ kỹ thuật, UUID thô và ngôn ngữ pha trộn Anh–Việt**
  - **Vị trí:** `frontend/src/matching.tsx:16-66,91-98`, `frontend/src/pairMatching.tsx:38,46,128`, `frontend/src/main.tsx:70,181,320-358`, `frontend/src/jobs.tsx:407-431`, `frontend/src/publicResumes.tsx:24-71`, `frontend/src/community.tsx:167`, `frontend/src/communityReports.tsx:25`, `frontend/src/api.ts:56-58`
  - **Mô tả vấn đề:** Nhiều chỗ rò rỉ `BASELINE L09`, `retrieval_score`, `term overlap`, `provenance`, `parserVersion`, `methodVersion`, `segmentId`, `simple-v1`, `revision`, `purge`, `rubric thử nghiệm`, `Candidate`/`Recruiter`, `#{c.parentId.slice(0,6)}`, `Mã: {detail.report.id}`.
  - **Yêu cầu tinh chỉnh:** Việt hóa hoàn toàn câu chữ giao diện người dùng (`Candidate` → `Ứng viên`, `Recruiter` → `Nhà tuyển dụng`, `BASELINE L09` → `Gợi ý theo từ khóa`, `purge` → `xóa vĩnh viễn`, `#{c.parentId.slice(0,6)}` → `Trả lời @tên_tác_giả`); gom toàn bộ mã kỹ thuật (`methodVersion`, `parserVersion`, `UUID`, `errorCode`) vào khối thu gọn `<details><summary>Thông tin kỹ thuật</summary>...</details>` chỉ dùng khi cần tra cứu.
  - **Kiểm chứng:** Đọc rà soát toàn bộ giao diện không còn thuật ngữ lập trình hay tiếng Anh rời rạc ở copy tiếng Việt.

---

## Nhóm 5 — Hạn chế hệ thống, Dữ liệu đánh giá AI, Kiểm thử & Vận hành (`LIM-01` .. `LIM-07`)

- [x] **LIM-01 (P1)** *(Đã hoàn thành 27/11/2026)* — **Chạy lại toàn bộ Playwright E2E và bổ sung test cho các tính năng P1–P3 mới**
  - **Vị trí:** `frontend/e2e/`, `scripts/test-frontend-regression.py`
  - **Mô tả vấn đề:** Sau khi đồng bộ nhánh `feat/post-freeze-p1-p3-20261121` (231 backend tests, 42 FE unit tests), bộ E2E (`22` bài cũ) **chưa được chạy lại** và chưa có spec E2E cho các tính năng mới (`#home`, `#community`, `BusinessIdentity`, `QuickMatch`, `AnalysisHistory`, `WeeklyRoadmap`).
  - **Yêu cầu tinh chỉnh:** Cập nhật các spec E2E bị ảnh hưởng sau khi tinh chỉnh giao diện (`SYNC-*`, `FE-*`) và bổ sung spec E2E cho luồng mới; chạy trọn bộ qua `python scripts/test-frontend-regression.py`.
  - **Kiểm chứng:** Run `20261127-032340-8554` đạt `completed: true`: `240/240` backend PG tests, `12 files / 47/47` Vitest unit tests, `lint/typecheck/build` exit 0, và `26/26` Playwright E2E pass trên môi trường sạch (bao gồm `e2e/p1-p3-flows.spec.ts` và helper xác minh `BusinessIdentity` qua Admin seed).

- [ ] **LIM-02 (P3)** — **Mở rộng dữ liệu đánh giá AI vượt phạm vi pilot hẹp `pilot_2cv_4jd_limited`**
  - **Vị trí:** `project-docs/evaluation/`, `project-docs/report/quality-acceptance-2026-10-30.md`
  - **Mô tả vấn đề:** Pilot hiện tại chỉ có **2 CV tiếng Anh × 4 JD** (0 CV tiếng Việt, `human_direct_label_review = 0`), Exact CV F1 = `0.6232`, Partial F1 chưa đo, và LLM rerank `nDCG@3 = 0.9170` thấp hơn baseline `1.0`.
  - **Yêu cầu tinh chỉnh:** Bổ sung tập CV tiếng Việt thực tế/ẩn danh hợp lệ, thực hiện gán/rà nhãn có người kiểm chứng (`human_direct_label_review > 0`), đo thêm Partial P/R/F1 và rà lại prompt/rubric xếp hạng CV→JD.
  - **Kiểm chứng:** Có manifest đánh giá và báo cáo thực nghiệm mới tách biệt khỏi pilot 30/10.

- [x] **LIM-03 (P3)** *(Đã hoàn thành phần dọn file `failed` tự động 27/11/2026)* — **Tự động hóa lịch sao lưu (14 ngày) và dọn file lỗi (7 ngày)**
  - **Vị trí:** `backend/CareerGap.Api/Files/OrphanFileCleanupService.cs`, `scripts/backup-local.ps1`, `scripts/expire-local-backups.ps1`, `scripts/queue-expired-failed-files.ps1`
  - **Mô tả vấn đề:** Các tác vụ hết hạn backup 14 ngày và dọn file `failed` sau 7 ngày mới là script PowerShell chạy tay trên local, chưa có worker quét dọn file `failed` tự động trên server.
  - **Kết quả thực hiện:** Đã tích hợp chính sách `FailedRetentionPeriod = 7 ngày` trực tiếp vào `OrphanFileCleanupService` / `OrphanFileCleanupWorker` (tự động dọn `StoredFile` trạng thái `failed` quá 7 ngày không có tham chiếu trên cả Local và S3/R2, pass test `ExpiredFailedFilesAreDeletedAfterSevenDaysWhileRecentFailedFilesAreKept`); riêng backup PostgreSQL offsite vẫn vận hành theo hạ tầng DB quản lý.

- [x] **LIM-04 (P3)** *(Đã hoàn thành mở rộng bộ lọc ẩn liên hệ 27/11/2026; OCR để ngoài phạm vi đồ án)* — **Nâng cấp độ phủ ẩn thông tin liên hệ trên CV công khai & hỗ trợ OCR**
  - **Vị trí:** `backend/CareerGap.Api/Files/RedactedResumeBuilder.cs`, `backend/CareerGap.Api/Files/DocumentParser.cs`
  - **Mô tả vấn đề:** Hệ thống chưa hỗ trợ OCR cho PDF scan/ảnh; bản CV ẩn liên hệ cần phủ rộng hơn các định dạng liên hệ phổ biến ở Việt Nam.
  - **Kết quả thực hiện:** Đã mở rộng `RedactedResumeBuilder` nhận diện và ẩn tên miền `.com.vn`/`.edu.vn`/`.vercel.app`/`.github.io`, liên kết `zalo.me`/`t.me`/`fb.com`, handle liên hệ `Zalo/Telegram/Skype/WhatsApp` và SĐT có khoảng trắng không ngắt dòng (`\u00A0`), kiểm chứng bởi `LIM04_RedactedResumeScrubsVietnameseDomainsSocialHandlesAndFormattedPhones`. OCR tiếp tục giữ ngoài phạm vi đồ án.

- [~] **LIM-05 (P3)** *(Đã hoàn thiện hạ tầng Vercel + Render + Cloudflare R2 + `DP_CERT_BASE64` + Proxy/SPA/Security Headers 27/11/2026; còn cấu hình domain email Resend)* — **Điều kiện sẵn sàng triển khai Cloud / Production**
  - **Vị trí:** `Dockerfile`, `frontend/vercel.json`, `frontend/index.html`, `frontend/public/robots.txt`, `backend/CareerGap.Api/Program.cs`
  - **Mô tả vấn đề:** Cần hoàn thiện cấu hình hạ tầng cloud, proxy headers, SPA routing, bảo mật và email domain khi triển khai thực tế.
  - **Kết quả thực hiện:** Đã triển khai frontend lên `https://careergapai.vercel.app` và API lên Render kết nối Cloudflare R2 + chứng thư `DP_CERT_BASE64`, bổ sung `ForwardLimit = 3` cho `ForwardedHeadersOptions`, SPA fallback + security headers trong `vercel.json`, `robots.txt`, Open Graph và khắc phục toàn bộ 15 hạng mục kiểm toán Chrome DevTools (`WEB-01..15`: sửa link `#recruiter-company`/`#private-resumes`/`#shared-profile`, mở tự động `#jobs?quickMatch=1`, giảm CLS `#home`, lazy-mount + chia nhỏ bundle JS, đạt chuẩn ARIA/Heading/Contrast/Form `id-name`). Phần xác minh domain gửi mail thật trên Resend (`Recovery:BaseUrl` / SPF-DKIM) và seed dữ liệu mẫu cloud tùy chủ dự án bật khi cần.

- [x] **LIM-06 (P2)** *(Đã hoàn thành 21/11/2026)* — **Dọn dẹp file rác & untrack cache hệ thống lỡ commit sau khi chủ dự án duyệt `cleanup-candidates.md`**
  - **Vị trí:** `cleanup-candidates.md`, `frontend/%SystemDrive%/` (4 file đang tracked trong Git)
  - **Mô tả vấn đề:** Thư mục `frontend/%SystemDrive%/ProgramData/Microsoft/Windows/Caches/` (4 file `.db`) đang bị Git track nhầm; nhiều thư mục cache `.playwright-cli`, `l01-l09-validation`, `bin/obj`, tham chiếu trùng lặp đang chiếm hàng GB ổ đĩa.
  - **Kết quả thực hiện:** Đã untrack `frontend/%SystemDrive%` qua `git rm -r --cached` và xóa toàn bộ các mục Nhóm A, Nhóm B (giữ `.local/frontend-step3-20261101-141707-55ce`) và Nhóm C (giữ `output/design-guide-20261029` và cặp `test-frontend-regression.py` / `test_frontend_regression.py`) theo phê duyệt của chủ dự án; giữ nguyên toàn bộ Nhóm D.
  - **Kiểm chứng:** Tất cả đường dẫn Nhóm A/B/C đã xóa khỏi đĩa, toàn bộ Nhóm D vẫn còn nguyên vẹn.

- [x] **LIM-07 (P2)** *(Đã hoàn thành 27/11/2026)* — **Chốt hồ sơ phát hành riêng cho nhánh sau tinh chỉnh (giữ nguyên Defense Baseline `release20261031`)**
  - **Vị trí:** `project-docs/evaluation/`, `project-docs/report/`
  - **Mô tả vấn đề:** Hiện các file `project-docs/evaluation/current-status.json` và `source-freeze-2026-11-01.json` chỉ phản ánh bản đóng băng 01/11 (`release20261031`), chưa có manifest đánh dấu riêng cho bản nâng cấp sau khi hoàn tất `taskboard_v2.md`.
  - **Yêu cầu tinh chỉnh:** Giữ nguyên các file đóng băng lịch sử của `release20261031`; tạo file báo cáo/manifest kiểm chứng mới cho bản nhánh mở rộng sau khi hoàn tất các task P0–P2 trong tài liệu này.
  - **Kiểm chứng:** Đã tạo hồ sơ riêng [post-freeze-ui-sync-2026-11-27.md](project-docs/report/post-freeze-ui-sync-2026-11-27.md) và [post-freeze-ui-sync-2026-11-27.json](project-docs/evaluation/post-freeze-ui-sync-2026-11-27.json); các tệp Defense Baseline `release20261031` (`41b82c6`, 222 file đóng băng) giữ nguyên hoàn toàn.
