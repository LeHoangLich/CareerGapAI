# Schema và hợp đồng hiện thực tại nhánh `feat/post-freeze-p1-p3-20261121` (và lịch sử baseline)

> **Cập nhật nhánh mở rộng sau đóng băng (20–26/11/2026 — 32 migrations):** Trên cơ sở **19 migrations** của Defense Baseline (`release20261031`), nhánh `feat/post-freeze-p1-p3-20261121` (`v2026.11.21-p1-p3`) đã áp dụng thêm **13 migrations** (tổng **32 migrations**):
> - `20261102130821_CommunityNetwork`: `community_profiles`, `community_posts`, `community_tags`, `community_post_tags`, `community_comments`, `community_reactions`, `community_bookmarks`, `community_follows`, `community_views`, `community_notices`, `community_images`, `community_reports`.
> - `20261103052341_ApplicationCoverLetter`: cột `cover_letter` (`varchar(5000)`) trên `applications`.
> - `20261103160632_ReportReviewWorkflow`: cập nhật `ck_content_report` cho phép trạng thái trung gian `reviewed`.
> - `20261103180858_SavedWeeklyRoadmaps`: bảng `saved_roadmaps` (FK cascade từ `match_inputs`; ban đầu cột PascalCase, đã chuẩn hóa về `snake_case` tại migration #32).
> - `20261103182529_CompanyLogos`: cột `logo_image_id` trên `companies` tham chiếu `community_images`.
> - `20261104144420_JobTags`: cột `tags` (`text[]`) kèm GIN index trên `jobs`.
> - `20261104153539_RecruiterBusinessIdentity`: bảng `business_identities` (`user_id`, `company_id`, `legal_name`, `tax_code`, `bound_tax_code`, `domain`, `status`, `review_reason`, `row_version`, `submitted_at`, `reviewed_by`, `reviewed_at`).
> - `20261104165201_CommunityCareerVisibility`: cột `show_career` trên `community_profiles`.
> - `20261120111358_JobCapacityAndSoftDelete`: các cột `max_applications`, `archived_by_role`, `previous_status` và cập nhật `ck_job_listing_state` trên `jobs`.
> - `20261120120328_AdminUserLastLogin`: cột `LastLoginAt` trên `AspNetUsers`.
> - `20261120122235_StageP2AiAndLearningExtensions`: mở rộng `ai_jobs.job_type` thêm `rewrite_jd`; bổ sung `career_context`, `quick_match_jd_id` (và cho phép `job_version_id`/`job_revision_id` nullable) trên `match_inputs`; tạo các bảng `quick_match_jds`, `job_rewrite_inputs`, `job_rewrite_results`, `analysis_history_hides`.
> - `20261120125556_StageP3DiscoveryFeatures`: tạo `job_views` và `community_searchs` (đã đổi tên bảng thành `community_searches` tại migration #32).
> - `20261126134034_NormalizeSavedRoadmapsAndCommunitySearches` (migration #32, `BE-13`): chuẩn hóa cột `saved_roadmaps` sang `snake_case` (`pair_id`, `plan`, `completed`, `row_version`, `updated_at`) và đổi tên bảng `community_searchs` thành `community_searches`.
> - **Ghi nhận kỹ thuật mở rộng & bảo trì:** Các bảng mở rộng (`quick_match_jds`, `job_rewrite_inputs`, `job_rewrite_results`, `community_searches`, `job_views`) đã được bổ sung vào luồng ẩn danh/xóa dữ liệu tại `AccountDeletionService.PurgeDatabaseRowsAsync`; tiến độ chi tiết theo dõi tại [taskboard_v2.md](../taskboard_v2.md).

> **D ngày 23/10:** 19 migrations; các bảng thực mới `writing_inputs`, `writing_results`, `writing_decisions`, `learning_requests`, `content_reports`, `admin_audits`, `email_deliverys`; `suggest_cv` trong `ai_jobs`. Không có bảng learning_resources hay taxonomy đầy đủ như thiết kế đích. [Hợp đồng D](phase-05-improvement-and-administration.md), [report D](report/stage-d-2026-10-23.md). Ma trận baseline dưới giữ theo thời điểm b392ef0, không là hiện trạng D.

> **C ngày 22/10 — migration 16:** đã có `ai_jobs.job_type` (`extract_cv`/`match_pair`), `match_inputs`, `match_results`, FK owner/version/revision/context và trigger bất biến. Matching ghim CV/JD snapshot cùng revision xác nhận tại lúc nộp trong input, không thêm/backfill cột revision application. User quota được tính trên attempts/reservations hiện có. Chưa có parent recommendation job/`recommendation_items`, skill projection hay taxonomy. [Hợp đồng C](phase-04-matching.md), [kiểm chứng migration và report](report/stage-c-2026-10-22.md). Ma trận baseline bên dưới giữ nguyên giá trị lịch sử.

> B/P04–P09 cập nhật: full CV schema v3 và JD v2 nằm trong các bảng revision đã có; builder v2 và v3 confirmed được projection self/public theo provenance; không thêm bảng/cột/migration. [Hợp đồng mới](phase-03-extraction-contract.md), [report](report/stage-b-2026-10-16.md). Các giới hạn baseline b392ef0 bên dưới giữ theo thời điểm.

Đối chiếu ngày 16/10/2026 từ `Data/AppDb.cs`, `Data/Models.cs`, migrations và module/API liên quan. [Database design](04-database-design.md) là thiết kế đích; bảng này không phê duyệt tự tạo các bảng còn thiếu.

## Ma trận triển khai

| Nhóm | Hiện thực | Phần đích chưa có/khác biệt |
|---|---|---|
| Identity, DP | Bảng Identity framework và `DataProtectionKeys` | Không có bảng users/roles nghiệp vụ riêng; bảo vệ certificate production chưa kiểm chứng |
| Profile | `candidate_profiles`, `candidate_profile_visibility` | Details JSON có allowlist; chưa projection kỹ năng chuẩn |
| Recruitment | `companies`, `jobs`, `job_versions`, `saved_jobs`, `job_data_revisions` | Revision JD `local_extracted`/`user_confirmed`; chưa LLM JD đầy đủ |
| CV/files | `stored_files`, `resumes`, `resume_versions`, `resume_data_revisions`, `candidate_public_resumes` | Chưa skill taxonomy/projection; selection/application chưa pin revision; không backfill bằng revision mới |
| Applications | `applications`, `application_status_history`, `application_notes`, `job_candidate_blocks`, `notifications` | Pin CV/JD version, revision chưa pin; phân tích lại JD cũ/mới còn quyết định mở |
| Retention | `account_deletion_requests`, `account_deletion_objects`; retry fields và workers | Local backup expiry thủ công, chưa scheduler/offsite; failed file retention và sweep key ngoài metadata còn mở |
| AI/chi phí | `ai_method_versions`, `ai_jobs`, `budget_periods`, `budget_reservations`, `external_call_attempts`, `call_budget_allocations` | Chủ yếu extract CV skills; chưa job đa tác vụ, user quota hoặc bảng kết quả phân tích cặp |
| Matching/learning/admin | L09 on-demand; L12 diagnostic; evaluator manifest ngoài DB | Chưa `match_results`, `recommendation_items`, `learning_resources`, `content_reports`, `audit_events` |

## Hợp đồng enum thực dùng

Không đổi schema/nghiệp vụ để hợp thức hóa khác biệt. Các giá trị dưới là hợp đồng implementation đã xác minh, dùng khi xây API/UI phụ thuộc; mở rộng phải có quyết định và migration nếu cần.

| Trường/scope | Implementation | Khác thiết kế đích và ảnh hưởng |
|---|---|---|
| JD `employment_type` | `full_time`, `part_time`, `contract`, `internship`, `temporary` | Đích ghi `other`; gửi `other` hiện bị từ chối. Chưa xác nhận thêm `other` hoặc đổi `temporary` |
| JD `language` | `vi`, `en`, `vi-en` | Đích chung ghi `mixed`/`unknown`; không dùng enum chung cho mọi module |
| CV revision `origin` | `ai_extracted`, `user_confirmed`, `user_entered` | v1 skills; builder v2 `user_entered`, không phải AI extraction |
| JD revision `origin` | `local_extracted`, `user_confirmed` | Không phải `ai_extracted`; bộ tách local có nguồn/offset |
| CV version nguồn | `ResumeVersion` chưa có cột `source`/`origin` | Thiết kế có `uploaded`/`generated`; builder hiện phân biệt qua revision `user_entered` v2, không suy ra cột nguồn đã có |
| Budget service/unit | `Fake`/`test_credit`, `DeepSeek`/`usd` | Đích có service/scope/search khác; không coi quota Fake là USD hoặc user quota |
| AI job/method | Lịch sử b392ef0 gắn `ResumeVersionId` + `extract_cv`; sau C/D/mở rộng hỗ trợ `extract_cv`, `match_pair`, `suggest_cv`, `rewrite_jd` và `access_context` (`self`, `public_pool`, `application`) | Còn bảng `recommendation_items`, `learning_resources`, `audit_events` dùng cấu trúc riêng (`learning_requests`, `admin_audits`, `saved_roadmaps`) |

*(Lưu ý lịch sử b392ef0)* 15 migrations là mốc baseline giai đoạn A (hiện tại là **19 migrations** ở Defense Baseline `release20261031` và **31 migrations** trên nhánh mở rộng `feat/post-freeze-p1-p3-20261121`); không sửa migration cũ hoặc tạo bảng chỉ để khớp sơ đồ. Kiểm tra mới/nâng cấp và giới hạn Down theo từng test được ghi trong [báo cáo A](report/stage-a-2026-10-16.md).
