# GĐ1 — Hợp đồng, quyết định và vận hành

> API bổ sung và hợp đồng mới sau B: [GĐ3 extraction/builder](phase-03-extraction-contract.md), [kiểm chứng B](report/stage-b-2026-10-16.md). Các mô tả skills-only bên dưới giữ cho luồng v1 tương thích; v3 là contract riêng, không thay đổi snapshot/version đã lưu.

> Hiện trạng chuẩn và CI: [current-status](current-status.md). Hợp đồng schema/API thực dùng được đối chiếu ở [ma trận](schema-implementation-status.md); báo cáo các lượt cũ không thay cho [kiểm tra A](report/stage-a-2026-10-16.md).

Ngày 15/10/2026. Nguồn yêu cầu: [đặc tả](../project-docs/01-system-specification.md), [công nghệ](../project-docs/02-tech-stack-decisions.md), [database](../project-docs/04-database-design.md). Đây là mô tả lát triển khai local, không phê duyệt toàn bộ các đề xuất trong tài liệu gốc.

## API hiện có

Các request thay đổi `/api` cần cookie và header `X-CSRF-TOKEN`, kể cả login. Lấy token lại sau login/logout; không dùng token anonymous thay token user. JSON dùng camelCase; lỗi dùng ProblemDetails với title là mã công khai, không trả exception.

| Method/path | Input | Quyền và output |
|---|---|---|
| GET `/api/health` | Không | Anonymous, trạng thái tiến trình, không chứng minh DB/provider sẵn sàng |
| GET `/api/auth/csrf` | Không | Anonymous/current user, `{token}` và cookie CSRF |
| GET `/api/auth/me` | Không | `{authenticated,email,roles}` của phiên hiện tại |
| POST `/api/auth/login` | `{email,password}` | CSRF; cookie Identity, 204 hoặc 401; rate limit 10/phút theo IP (Testing: 1000) và lockout 5 lần |
| POST `/api/auth/logout` | Không | Phiên hợp lệ + CSRF; 204 |
| POST `/api/resumes` | Multipart `file` | Candidate; 201 `{id,versionId,fileId,title}`; tạo CV riêng mới/version 1 |
| GET `/api/resumes?page=1` | Page 1–10000 | Chính chủ; 20 mục/trang, không trả văn bản/bằng chứng |
| GET `/api/files/{id}` | UUID | Chính chủ, tài khoản active; download attachment; 404 cho người khác, không cấp signed URL |
| GET `/api/resume-versions/{versionId}/revisions` | UUID | Candidate chính chủ; revisions của đúng version, mới nhất trước; recruiter/khách không đọc được |
| POST `/api/resume-versions/{versionId}/revisions/manual` | `{baseRevisionNo,templateVersion,displayName,email,phone,location,headline,summary,skills,experience,education,projects,certifications}` | Candidate chính chủ; chỉ template `simple-v1`, schema v2, giới hạn độ dài/số mục; tạo `user_entered` revision bất biến, evidence ghi field path; cùng nội dung mới nhất trả revision cũ, stale base trả 409 |
| GET `/api/resumes/public/me` | Không | Candidate chính chủ; lựa chọn public đang hoạt động hoặc JSON `null` |
| POST `/api/resumes/public` | `{resumeVersionId,expectedRowVersion}` | Candidate chính chủ; publish/replace đúng version; 200 `ready/not_ready`, 409 stale state; dựng và kiểm tra file dẫn xuất trước khi gắn con trỏ |
| DELETE `/api/resumes/public/me?rowVersion=...` | Row version | Candidate chính chủ; 204, xóa lựa chọn public và thu hồi URL theo version hiện hành |
| GET `/api/public/resumes?page=1` | Page 1–10000 | Candidate thấy bản dẫn xuất `ready`; recruiter thấy toàn bộ lựa chọn public, tên/headline vẫn theo profile visibility |
| GET `/api/public/resumes/{candidateId}/{resumeVersionId}/file` | UUIDs | Candidate khác nhận file dẫn xuất hiện hành đã xác minh; recruiter nhận file gốc; stale version/khách/admin trả 404/403 |
| POST `/api/ai/jobs` | `{resumeVersionId,idempotencyKey}` UUID | Candidate sở hữu tài liệu; 202 `{id,status}`, Location; cùng key/input trả job cũ, khác input 409 |
| GET `/api/ai/jobs/{id}` | UUID | Chính chủ; status/outputJson/errorCode/version/provider/model/prompt/schema; không lộ usage nội bộ |
| GET `/api/recruiter/jobs/{jobId}/versions/{versionId}/revisions` | UUIDs | Recruiter owner; revision của đúng JD version, mới nhất trước |
| POST `/api/recruiter/jobs/{jobId}/versions/{versionId}/revisions/extract` | Không | Recruiter owner; tách yêu cầu theo dòng local, giữ source/evidence/offset; không gọi provider; trả revision hiện có nếu version đã có revision |
| POST `/api/recruiter/jobs/{jobId}/versions/{versionId}/revisions/{revisionId}/confirm` | `{baseRevisionNo,acceptedRequirements,userStatements}` | Recruiter owner; xác nhận đúng revision mới nhất, bỏ/chọn/phân loại các dòng hoặc thêm user statement; tạo revision con bất biến, stale trả 409 |
| POST `/api/jobs/{jobId}/applications` | `{resumeVersionId,submissionKey}` UUID | Candidate; pin CV/JD versions, tạo application/history/notifications cùng transaction; 201 lần đầu, 200 cùng key/hash, 409 key khác hash hoặc đơn hiệu lực đã có |
| GET `/api/applications/mine` | `page`, `pageSize` | Candidate chính chủ; trang đơn của mình |
| GET `/api/jobs/{jobId}/applications` | `page`, `pageSize` | Recruiter owner đúng job; metadata theo snapshot, đơn kết thúc quá 90 ngày bị ẩn |
| GET `/api/applications/{id}` | UUID | Candidate là chủ đơn hoặc recruiter owner trong hạn; 404 ngoài quyền |
| GET `/api/applications/{id}/resume` | UUID | Candidate chính chủ; recruiter owner khi đơn hiệu lực hoặc rejected/withdrawn chưa quá 90 ngày; trả đúng file version đã nộp, 404 ngoài quyền/hết hạn |
| PUT `/api/applications/{id}/status` | `{rowVersion,status}` | Recruiter owner; ma trận đã xác nhận; application/history/notification atomic; stale token hoặc chuyển không hợp lệ trả 409 |
| POST `/api/applications/{id}/withdraw` | `{rowVersion}` | Candidate chính chủ; chỉ đơn còn hiệu lực; history và notification recruiter cùng transaction |
| GET `/api/applications/{id}/history` | UUID | Candidate chính chủ hoặc recruiter owner trong hạn; chỉ status/thời điểm, không lộ note |
| GET/POST `/api/applications/{id}/notes` | GET `page`; POST `{body}` | Recruiter owner trong hạn; note private, append-only, không có trong DTO/thông báo candidate |
| PUT/DELETE `/api/jobs/{jobId}/candidates/{candidateId}/block` | UUIDs trong path | Recruiter owner; idempotent, phạm vi đúng tin; apply/block tuần tự trên cùng hàng job |
| GET `/api/notifications` | `page` | Chỉ notification của user hiện tại, tối đa 20/trang |
| PUT `/api/notifications/{id}/read` | Không | Chỉ recipient; idempotent, 204 |

Mã tiêu biểu: 400 `csrf_invalid`, 401 invalid session/credentials, 403 sai role, 404 tài nguyên không thuộc quyền/hết retention; 409 conflict/idempotency, 422 lỗi file; 429 `quota_exhausted`; 503 `ai_disabled`/`provider_not_configured`. Các response API `no-store`. OpenAPI chỉ bật development/testing.

CV vẫn private theo mặc định, không tự public khi upload hoặc nộp ứng tuyển. Candidate chọn một version để publish; recruiter xem file gốc, candidate khác chỉ xem URL gắn candidate/version trỏ tới DOCX text-only dựng lại, đã quét email/điện thoại/URL nhận diện được và xác minh hash. Ảnh, hyperlink, metadata và file gốc không được chép vào derivative. Nếu chưa `ready`, candidate khác không nhận file gốc; ẩn/thay selection làm URL version cũ trả 404. Middleware kiểm tra account status/security stamp và role hiện tại mỗi request; recruiter mất quyền đọc đơn rejected/withdrawn sau 90 ngày. L06 triển khai ma trận sàng lọc recruiter owner đã xác nhận ngày 16/10, candidate withdraw, history/notification, note chỉ recruiter owner, block theo tin và nộp lại bằng application mới. Block không đổi/xóa application đang có; note không xuất hiện trong DTO hay notification candidate. L07 thêm kho public/CV derivative; L08 có request/cancel/purge local, retention và cleanup pending; chưa có bằng chứng production.

## Dữ liệu và job

- Một Web API/DbContext; migrations `Foundation`, `ImmutableDocuments`, `JobInputIntegrity`. Giữ tên Identity/DP theo framework; bảng nghiệp vụ snake_case.
- Candidate profile, files/resume versions, recruitment, applications, retention, matching, `resume_data_revisions` và `job_data_revisions`; 15 migrations tổng cộng đã áp dụng PostgreSQL local. FK ghép chặn file/CV/job/application/public selection/revision chéo version hoặc chủ. Object key ngẫu nhiên, hash xác minh sau lưu. Upload ngoài transaction; ready + resume/version lưu atomic. Application pin CV/JD versions và ghi status/history/notification atomic; note append-only và owner-only; block khóa theo job/candidate. Public selection khóa profile khi publish/hide, pin hash nguồn; derivative chỉ trở thành ready sau xác minh storage/hash/content. Pending/object mồ côi được giữ để đối soát; worker L08 dọn upload pending theo chính sách.
- Resume/JD versions và application snapshot bất biến bằng trigger; history append-only; ready file không sửa định danh nội dung. Job input và output thành công cũng được guard; không có API ghi đè snapshot. Candidate có thể thêm CV version vào collection cũ dưới row lock; application không backfill revision chưa tồn tại.
- L10 chuyển skills/evidence của worker thành `resume_data_revisions` bất biến cho đúng CV version. Job success và AI revision được ghi cùng transaction; Candidate xem source/evidence, chọn/bỏ kỹ năng và lưu thêm kỹ năng tự khai báo với `user_statement` tách biệt. Confirmation tạo child revision có người/thời điểm xác nhận; application/public selection chưa ghim revision. Đây mới là schema v1 cho kỹ năng, chưa phải full CV/JD extraction hoặc nghiệm thu GĐ3.
- L11 thêm `job_data_revisions` bất biến theo từng JD version. API recruiter owner tách các dòng trong `requirements_text`, lưu source và offset; bộ gợi ý từ khóa phân loại `required`/`optional`/`unspecified`, không gọi LLM. Recruiter chọn dòng, chỉnh phân loại, bỏ dòng hoặc tự khai báo yêu cầu; xác nhận tạo revision con, mục tự khai báo có `user_statement` riêng. Revision chỉ đọc được trong owner scope; chưa có bảng skill projection hoặc con trỏ current revision trên `jobs`. Đây là lát cấu trúc local, không phải trích xuất JD chất lượng đã đánh giá hay nghiệm thu GĐ3.
- L12 thêm consumer diagnostic hai chiều dùng revision `user_confirmed` mới nhất của đúng CV/JD version, sau khi kiểm tra người xác nhận, schema và evidence/offset. Candidate scope có thể đối chiếu skill `user_statement` của chính mình; recruiter scope cho CV public chỉ dùng skill `document_span`. Trả số yêu cầu có token overlap, tách required counts; không phát hành tên kỹ năng, composite score, ngưỡng hoặc thay điểm/thứ tự retrieval L09. Khi thiếu/không hợp lệ revision, diagnostic ghi trạng thái chưa đủ dữ liệu và baseline vẫn độc lập. Evaluator nhận projection optional được đánh dấu confirmed-by-owner, thống kê cặp có nhãn theo relevance cho candidate/public scope; không thay baseline metrics hay xếp hạng. Chưa có schema taxonomy/projection hoặc bằng chứng chất lượng trên nhãn thật.
- L13 có CV builder candidate-entered với các nhóm liên hệ, headline/summary, skills, experience, projects, education, certifications. Browser tạo preview/download bằng `@react-pdf/renderer` + Noto Sans; save upload PDF qua parser/hash/private storage hiện có, rồi POST manual revision schema v2 với evidence chỉ có field path + `user_statement`, `confirmed_by/at`, hash và template version. Reopen nạp revision vào form; save sau sửa tạo resume file version mới, không ghi đè bản cũ. Revision API khóa đúng file version, candidate owner-only, base revision conflict và retry nội dung giống latest idempotent. Không thêm migration; `resume_versions` chưa lưu editor snapshot/template columns thiết kế đích, cấu trúc nằm trong immutable revision JSON v2. L12 diagnostic/evaluator chỉ tiêu thụ confirmed skill schema v1 nên bỏ qua builder schema v2; AI extraction vẫn chỉ có skills. Một template `simple-v1` là probe reviewable, chưa chốt gallery.
- Chốt method theo hash prompt/schema/parser/provider/model/parameters, ghim CV version. Backend chỉ nhận tối đa 20 kỹ năng, kiểm tra tên/quote, ref đoạn và substring nguồn; không tuyên bố đây là đánh giá ngữ nghĩa hoàn chỉnh.
- Admission transaction ngắn có advisory lock; khóa budget trước giữ chỗ cho tối đa 2 lần gọi. Worker `FOR UPDATE SKIP LOCKED`, lease/generation, một job tại một thời điểm; timeout xử lý async 70 giây, lease tối thiểu 120 giây và gia hạn trước dispatch. Idle backoff tối đa 60 giây, có tín hiệu enqueue trong process. Không có ping giữ hosting thức.
- External call được ghi in-flight trước dispatch. Restart có thể để lại outcome_unknown; retry tạo attempt mới trong khoản giữ sẵn. Worker cũ không publish nhưng vẫn được settle usage. Settlement khóa attempt/reservation/budget, tính một lần; timeout giữ allocation chưa rõ, chỉ trả phần chắc chắn chưa dùng.
- Fake dùng `test_credit` riêng, 1 đơn vị/lần gọi; không phải USD hoặc hóa đơn. DeepSeek dùng numeric USD, không cộng fake vào chi phí thật. Chưa có user-scope quota, UI quản trị ngân sách hoặc công cụ đối soát provider; global quota là mức tối thiểu hiện tại.
- Pending job qua cuối kỳ không được dispatch: kết thúc `budget_period_expired`, trả phần chưa dùng, giữ khoản chưa rõ. Người dùng cần job/key mới trong kỳ mới. Đây là lựa chọn fail-closed của prototype, chưa tự chuyển reservation sang kỳ khác như phương án đầy đủ của thiết kế 04.
- Schema là lát tối thiểu, chưa toàn bộ cột/enum/row_version của thiết kế 04. Các mutation đang có dùng khóa transaction/unique và trigger; trước API cập nhật profile/trạng thái GĐ2 phải bổ sung concurrency token, không dùng hiện trạng này thay quy tắc chống ghi đè của thiết kế.

## File và giới hạn còn phải kiểm chứng

4 MB/file, 20 trang PDF, 24.000 ký tự, 500 segment; DOCX tối đa 512 ZIP entries/20 MB sau giải nén/1 MB ký tự mỗi XML part. PDF dùng word extractor, Docstrum và reading-order detector; DOCX giữ thứ tự đoạn/bảng trong body. Không chạy macro/tải external resources.

Parser kiểm tra thời gian giữa các segment, cancellation giữa trang/đoạn. **Chưa có hard timeout cô lập process** cho một thao tác đồng bộ trong thư viện, chưa đọc đầy đủ header/footer DOCX; cần đo trên bộ mẫu khó trước khi nhận file không tin cậy trên cloud. Bộ giả nhiều cột không chứng minh parser tốt trên mọi bố cục.

Storage local chỉ development/testing. Adapter S3 tạo object `If-None-Match: *`, đọc có giới hạn; chưa được thử trên private bucket thật. Recruiter tải CV snapshot qua kiểm tra owner job/status/retention ở backend; endpoint file ID trực tiếp vẫn chỉ chính chủ. L08 đã có OrphanFileCleanupWorker dọn pending không tham chiếu sau 24 giờ, kiểm tra tham chiếu và retry; chưa có API xóa CV tùy ý. File failed chưa chốt retention; object key ngoài metadata chưa được sweep. Không xóa snapshot ngoài chính sách.

## Cấu hình production — hoãn, chỉ tham khảo cho giai đoạn sau

**Từ 15/10/2026 đã dừng deploy.** Không thực hiện cấu hình production, migration cloud hoặc redeploy theo phần dưới cho đến khi sản phẩm hoàn thiện localhost và người dùng yêu cầu mở lại. Nền tảng hiện tại dùng local PostgreSQL/storage và fake; hướng dẫn chạy ở README.

Không gửi secrets qua chat hoặc `VITE_*`; đặt trong secret store của host:

- `ConnectionStrings__Database`: runtime PostgreSQL 16, TLS, quyền DML riêng; dùng direct credential riêng cho migrations và backup. API không gọi Migrate lúc startup.
- `Storage__Provider=S3`, `Storage__Endpoint` HTTPS, `Storage__Bucket` private, `Storage__AccessKey`, `Storage__SecretKey`. Chưa chứng minh Neon/S3 tương thích của tài khoản thật.
- `DataProtection__CertificatePath`, `DataProtection__CertificatePassword`: mount PFX riêng, bảo vệ DB key ring; giữ application name CareerGapAI. Development lưu key DB chưa mã hóa bằng chứng thư, không đưa cấu hình này lên Internet.
- `AllowedHosts` là hostname demo thực tế; `ASPNETCORE_HTTP_PORTS` khớp port nền tảng. Production cookie HttpOnly/Secure/SameSite Lax; HTTPS termination phải được kiểm chứng ở host/proxy. Chưa cấu hình trust forwarded headers cho provider chưa biết.
- `Ai__Provider=DeepSeek`, `Ai__Enabled=true`, `Ai__ApiKey`, model và giá input/output trên một triệu token, `PricingVerifiedAt` ISO UTC trong 31 ngày, `PeriodStart/PeriodEnd` theo chu kỳ provider, `MonthlyLimit` >0 và ≤3 USD (phân bổ thử theo tài liệu 02). Mặc định limit=0 và Fake; thiếu cấu hình thì chặn lời gọi thật. Giảm Enabled ngừng dispatch mới; request đã gửi provider vẫn có thể phát sinh phí.

Giá/model phải kiểm tra lại tài liệu/provider trước bật; không hardcode giá cũ. Client cố định host DeepSeek HTTPS, không cho client chọn URL/model tùy ý; max input/output và một retry. Giá tính bảo thủ từ prompt token toàn phần, chưa tối ưu cache discount; đối chiếu dashboard trước kết luận chi phí thực.

Khi đã được mở lại deploy, người thực hiện tự review và deploy image từ bản qua CI; chạy script/bundle migration bằng quyền riêng. Thử HTTPS login/logout/CSRF/upload; file/cookie/job sau redeploy; một DeepSeek thật trong quota. Nếu Vercel không phù hợp tài khoản cá nhân, dùng cùng image phục vụ React static; cold start vẫn cần đo. Ngày 15/10/2026 người dùng đã tạo/publish [repository](https://github.com/LeHoangLich/CareerGapAI); [CI run 38718678231](https://github.com/LeHoangLich/CareerGapAI/actions/runs/38718678231) của `bdb3aeb` đã success cho frontend/backend/Docker. Thay đổi local sau đó chưa có CI run mới; không tự push thay người dùng. Chưa có public demo.

`compose.smoke.yaml` kiểm chứng fallback cùng origin ở localhost:9080, kết nối DB nội bộ cổng 5432. Đây là Testing/Fake/Local storage. Smoke E2E phải chọn file ID từ response upload mới, không chọn theo tên trùng với file trước đây lưu trên filesystem Windows. Bản ghi file cũ không có object ở container phải trả file_unavailable; không tạo file rỗng hoặc xem đó là storage bền vững.

Backup: DB gồm DP key ring + object private + chứng thư/khóa giải mã, tách quyền đọc. Thử restore trên môi trường cô lập rồi kiểm tra cookie/file/job. Rollback app không khôi phục dữ liệu; chưa chạy full cloud backup/restore hoặc xoay chứng thư.

## Quyết định nghiệp vụ

**Đã xác nhận ngày 15/10/2026:** UI tiếng Việt, CV/JD Việt–Anh; khách chỉ xem JD, CV yêu cầu đăng nhập; admin không mặc định đọc private; đăng ký một role Candidate/Recruiter, một recruiter owner/công ty. Ứng viên khác chỉ xem bản CV ẩn liên hệ, không fallback file gốc. Recruiter còn đọc snapshot sau rút trong hạn 90 ngày; nộp lại sau rút/bị loại nếu không có đơn còn hiệu lực và không bị chặn ở chính tin đó. Yêu cầu xóa tài khoản thu hồi quyền ngay, cửa sổ hủy 7 ngày, purge/ẩn danh rồi backup hết hạn thêm tối đa 14 ngày; theo [chính sách lưu/xóa](phase-01-data-retention.md).

Các quyết định này không đồng nghĩa đã triển khai purge production. L02 đăng ký/profile, L03 company/JD, L04 CV collection/version, L05 application/snapshot, L06 screening, L07 CV công khai/bản dẫn xuất, L08 purge/retention và L09 matching đã được triển khai local. L10 thêm revision kỹ năng CV, L11 thêm revision yêu cầu theo JD version; L12 thêm diagnostic overlay giới hạn từ revision đã xác nhận; L13 thêm builder/PDF schema v2 local. Application và public selection vẫn chỉ pin CV/JD version, chưa lưu revision ID; diagnostic không thay baseline L09 và chưa có score/ranking từ revision. AI extraction ngoài skills v1, gallery template cuối và rubric/ngưỡng/quy mô đánh giá còn mở đúng tại các mốc tương ứng; không hỏi lại chính sách đã xác nhận.

**Dịch vụ chưa cung cấp:** người dùng sẽ bổ sung sau qua [checklist S01–S12](phase-01-services-checklist.md). Không suy ra chưa có tài khoản, không tự tạo/mua hoặc đọc secrets; phần độc lập tiếp tục được thực hiện.

Search/email vẫn chỉ có interface và fake (search trả rỗng, không bịa URL; fake email không gửi thật). L02 đã bổ sung API/UI khôi phục mật khẩu bằng token Identity dùng một lần; liên kết chỉ được đưa vào hộp thư giả trong Development/Testing, mailbox Development giới hạn loopback. Chưa có email adapter thật hoặc bằng chứng giao nhận. Người phụ trách cần xác nhận Tavily quota/điều khoản, DNS/domain và sender; chạy một search kỹ năng không PII, ghi credits; kiểm chứng gửi email thật riêng trước khi công nhận sản phẩm hoàn thiện. Không tự mua domain hay bật gói có phí.
