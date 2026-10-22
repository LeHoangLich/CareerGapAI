# Schema và hợp đồng hiện thực tại baseline b392ef0

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
| AI job/method | Model hiện gắn `ResumeVersionId`; phương pháp extract skills | Các `job_type`, `access_context`, pair/result trong thiết kế chưa được triển khai đầy đủ |

15 migrations là baseline hiện tại; không sửa migration cũ hoặc tạo bảng chỉ để khớp sơ đồ. Kiểm tra mới/nâng cấp và giới hạn Down theo từng test được ghi trong [báo cáo A](report/stage-a-2026-10-16.md).
