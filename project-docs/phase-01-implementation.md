# GĐ1 — Hợp đồng, quyết định và vận hành

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
| POST `/api/ai/jobs` | `{resumeVersionId,idempotencyKey}` UUID | Candidate sở hữu tài liệu; 202 `{id,status}`, Location; cùng key/input trả job cũ, khác input 409 |
| GET `/api/ai/jobs/{id}` | UUID | Chính chủ; status/outputJson/errorCode/version/provider/model/prompt/schema; không lộ usage nội bộ |

Mã tiêu biểu: 400 `csrf_invalid`, 401 invalid session/credentials, 403 sai role, 404 tài nguyên không thuộc quyền; 422 lỗi file; 429 `quota_exhausted`; 503 `ai_disabled`/`provider_not_configured`. Các response API `no-store`. OpenAPI chỉ bật development/testing.

Luồng hiện chỉ self/private: recruiter/admin không có quyền đọc CV riêng của ứng viên khác. Middleware kiểm tra account status/security stamp và role hiện tại mỗi request; worker kiểm tra account trước gửi và trước publish. Không có public CV, application hoặc API đổi vai trò trong GĐ1.

## Dữ liệu và job

- Một Web API/DbContext; migrations `Foundation`, `ImmutableDocuments`, `JobInputIntegrity`. Giữ tên Identity/DP theo framework; bảng nghiệp vụ snake_case.
- Candidate profile tối thiểu, stored_files, resumes/resume_versions; FK ghép chặn file/CV/job chéo chủ. Object key ngẫu nhiên, hash xác minh sau lưu. Upload ngoài transaction; ready + resume/version lưu atomic. Pending/object mồ côi được giữ để đối soát, chưa có tác vụ tự dọn/xóa.
- Version/method bất biến bằng trigger; ready file không sửa định danh nội dung. Job input và output thành công cũng được guard; không có API ghi đè snapshot. Mỗi upload tạo CV mới, chưa có thao tác thêm version cho CV cũ nên không cấp MAX+1.
- Job chỉ là **extract probe**, không matching/điểm hoặc revision GĐ3. Kết quả thử skills/evidence lưu `ai_jobs.output_json` và schema; không được coi là revision đã xác nhận. Sang GĐ3 phải tạo bảng revision/provenance đúng thiết kế và giữ bản AI trước chỉnh sửa.
- Chốt method theo hash prompt/schema/parser/provider/model/parameters, ghim CV version. Backend chỉ nhận tối đa 20 kỹ năng, kiểm tra tên/quote, ref đoạn và substring nguồn; không tuyên bố đây là đánh giá ngữ nghĩa hoàn chỉnh.
- Admission transaction ngắn có advisory lock; khóa budget trước giữ chỗ cho tối đa 2 lần gọi. Worker `FOR UPDATE SKIP LOCKED`, lease/generation, một job tại một thời điểm; timeout xử lý async 70 giây, lease tối thiểu 120 giây và gia hạn trước dispatch. Idle backoff tối đa 60 giây, có tín hiệu enqueue trong process. Không có ping giữ hosting thức.
- External call được ghi in-flight trước dispatch. Restart có thể để lại outcome_unknown; retry tạo attempt mới trong khoản giữ sẵn. Worker cũ không publish nhưng vẫn được settle usage. Settlement khóa attempt/reservation/budget, tính một lần; timeout giữ allocation chưa rõ, chỉ trả phần chắc chắn chưa dùng.
- Fake dùng `test_credit` riêng, 1 đơn vị/lần gọi; không phải USD hoặc hóa đơn. DeepSeek dùng numeric USD, không cộng fake vào chi phí thật. Chưa có user-scope quota, UI quản trị ngân sách hoặc công cụ đối soát provider; global quota là mức tối thiểu hiện tại.
- Pending job qua cuối kỳ không được dispatch: kết thúc `budget_period_expired`, trả phần chưa dùng, giữ khoản chưa rõ. Người dùng cần job/key mới trong kỳ mới. Đây là lựa chọn fail-closed của prototype, chưa tự chuyển reservation sang kỳ khác như phương án đầy đủ của thiết kế 04.
- Schema là lát tối thiểu, chưa toàn bộ cột/enum/row_version của thiết kế 04. Các mutation đang có dùng khóa transaction/unique và trigger; trước API cập nhật profile/trạng thái GĐ2 phải bổ sung concurrency token, không dùng hiện trạng này thay quy tắc chống ghi đè của thiết kế.

## File và giới hạn còn phải kiểm chứng

4 MB/file, 20 trang PDF, 24.000 ký tự, 500 segment; DOCX tối đa 512 ZIP entries/20 MB sau giải nén/1 MB ký tự mỗi XML part. PDF dùng word extractor, Docstrum và reading-order detector; DOCX giữ thứ tự đoạn/bảng trong body. Không chạy macro/tải external resources.

Parser kiểm tra thời gian giữa các segment, cancellation giữa trang/đoạn. **Chưa có hard timeout cô lập process** cho một thao tác đồng bộ trong thư viện, chưa đọc đầy đủ header/footer DOCX; cần đo trên bộ mẫu khó trước khi nhận file không tin cậy trên cloud. Bộ giả nhiều cột không chứng minh parser tốt trên mọi bố cục.

Storage local chỉ development/testing. Adapter S3 tạo object `If-None-Match: *`, đọc có giới hạn; chưa được thử trên private bucket thật. Không có delete API hoặc tự dọn file để tránh xóa snapshot ngoài chính sách đã chốt.

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

Các quyết định này chưa phải tính năng public CV/application/block/purge đã có trong prototype. GĐ2 cần migrations/DTO/quyền/kiểm thử tương ứng; các bổ sung bảng block và metadata bản CV ẩn liên hệ ở tài liệu 04 vẫn là thiết kế đề xuất. Còn cần hoàn thiện các chuyển trạng thái sàng lọc chi tiết ngoài hành vi đã chốt, chọn JD cũ/mới khi phân tích lại, số/mẫu CV và rubric/ngưỡng/quy mô đánh giá theo mốc tương ứng; không hỏi lại quyết định đã xác nhận.

**Dịch vụ chưa cung cấp:** người dùng sẽ bổ sung sau qua [checklist S01–S12](phase-01-services-checklist.md). Không suy ra chưa có tài khoản, không tự tạo/mua hoặc đọc secrets; phần độc lập tiếp tục được thực hiện.

Search/email hiện mới có interface và fake (search trả rỗng, không bịa URL; fake email không gửi hoặc log token). Chưa có real adapter/API khôi phục mật khẩu sản phẩm. Người phụ trách cần xác nhận Tavily quota/điều khoản, DNS/domain và sender; chạy một search kỹ năng không PII, ghi credits; kiểm thử logic khôi phục local ở GĐ2 và xác minh gửi email thật riêng trước khi công nhận sản phẩm hoàn thiện. Không tự mua domain hay bật gói có phí.
