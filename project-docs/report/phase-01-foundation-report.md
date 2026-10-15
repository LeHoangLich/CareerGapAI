# Báo cáo thực thi GĐ1 — Nền tảng, quyền và đánh giá

Ngày báo cáo: **15/10/2026**, múi giờ Asia/Saigon. Kết luận: **Chưa đạt M1**. Đã thực thi và kiểm chứng prototype local; chưa hoàn thành toàn bộ GĐ1.

Cập nhật theo xác nhận mới của người dùng: dự án do **1 người tự thực hiện**. Đã sửa tiêu chí nhân sự sang dựng lại theo README và tự review/CI; không còn điều kiện ba thành viên. Các kết quả kiểm thử bên dưới giữ đúng bằng chứng của lần chạy đã ghi, không coi việc sửa tài liệu là chạy lại kiểm thử.

Nguồn đối chiếu: [task GĐ1](../tasks/phase-01-foundation.md), [roadmap/M1](../../project-docs/03-roadmap.md), [hợp đồng và giới hạn triển khai](../phase-01-implementation.md), [kế hoạch đánh giá](../evaluation/phase-01-evaluation.md), [dependency/giấy phép](../dependency-licenses.md).

## 1. Repository và kết quả triển khai

Người dùng đã tạo/publish [LeHoangLich/CareerGapAI](https://github.com/LeHoangLich/CareerGapAI). Đã xác minh `origin/main` tại **bdb3aeb50bc89b7d5113a9f7f44d5efef0b62d7f** bằng `git ls-remote`. Working tree sạch khi bắt đầu lượt tiếp tục. Không tạo remote mới, commit hoặc push thay người dùng.

[GitHub Actions run 38718678231](https://github.com/LeHoangLich/CareerGapAI/actions/runs/38718678231) của commit đó đã completed/success, xác minh qua GitHub API. Cả ba job [frontend](https://github.com/LeHoangLich/CareerGapAI/actions/runs/38718678231/job/116367547264), [backend](https://github.com/LeHoangLich/CareerGapAI/actions/runs/38718678231/job/116367547675) và [docker](https://github.com/LeHoangLich/CareerGapAI/actions/runs/38718678231/job/116367938183) đều success. **Các thay đổi local sau bdb3aeb chưa có CI run mới**; kết quả local bên dưới không được gán cho commit đã publish.

Đã có:

- React/TypeScript/Vite; một Web API .NET 10 theo thư mục nghiệp vụ; một project test; PostgreSQL 16 local. SDK/package/Node được khóa, có lockfile và kiểm kê giấy phép.
- Cookie Identity, CSRF, login/logout/session, role/owner và account status; tạo tài khoản giả qua lệnh local có kiểm soát. Lượt tiếp tục bổ sung validation email/password null trả 400 thay vì 500.
- Upload PDF/DOCX private, kiểm tra cấu trúc/giới hạn, xác minh storage hash, metadata và CV version. File tải qua backend kiểm tra chính chủ; không có quyền đọc mọi CV cho recruiter/admin.
- Ba migrations: Foundation, ImmutableDocuments, JobInputIntegrity. FK ghép owner/version, ràng buộc và trigger bảo vệ nội dung bất biến; áp dụng migrations bằng bước riêng.
- Job PostgreSQL có idempotency/lease/generation, retry tối đa một lần, giữ chỗ ngân sách/attempt/allocation và đối soát; timeout giữ khoản chi phí chưa rõ. Fake mặc định; DeepSeek adapter có cấu hình chặn khi chưa đủ thông tin/giá/ngân sách.
- Giao diện upload/download/job, trạng thái lỗi/quota, nhãn mô phỏng rõ, polling có giới hạn; thử xuất PDF client với font Việt.
- Docker Linux phục vụ React static cùng .NET; CI config; README và `compose.smoke.yaml` cho phép lặp lại bài test cùng origin ở localhost:9080.
- Manifest bộ giả, kế hoạch gán nhãn/chia nhóm/đánh giá và danh sách cần trao đổi giảng viên. Chưa có đánh giá chất lượng AI.

## 2. Môi trường và kiểm tra đã chạy

Máy Windows/PowerShell: Git 2.45.1.windows.1, .NET SDK 10.0.401, runtime 10.0.12, Node 24.16.0, npm 11.13.0, Docker 29.5.3. PostgreSQL container **16.10-bookworm**, cùng major CI. Parser: PdfPig 0.1.16 + OpenXML 3.5.1. Không có psql native; dùng psql trong container. `dotnet --info` trong sandbox ban đầu báo lỗi quyền Service Control Manager; build/test thực tế đã chạy thành công.

Các lệnh từ root là `D:/CareerGapAI`; frontend là `D:/CareerGapAI/frontend`. `. ./scripts/dev-env.ps1` nạp cấu hình local bị Git bỏ qua, không in secrets. Những kiểm tra cần Docker/network/loopback đã chạy với quyền công cụ phù hợp sau khi sandbox chặn truy cập.

| Kiểm tra/lệnh | Thư mục/điều kiện | Kết quả thực tế |
|---|---|---|
| `git status --short`, `git branch -vv`, `git remote -v`, `git ls-remote --heads origin` | Root; network cho remote | main/remote đã xác minh; baseline bdb3aeb; thay đổi tiếp tục nằm local |
| GitHub API run/jobs 38718678231 | Read-only, repository đã publish | Frontend/backend/Docker success cho bdb3aeb |
| `dotnet restore ...`, `dotnet build ... --no-restore` | Root; SDK/package đã khóa | API và project test build được; 0 cảnh báo/lỗi ở lần build thành công |
| `dotnet test tests/CareerGap.Api.Tests/CareerGap.Api.Tests.csproj --no-restore --logger 'trx;LogFileName=foundation.trx'` | Root; `CAREERGAP_TEST_DATABASE` trỏ PostgreSQL local | **19 passed, 0 failed, 0 skipped**, lần chạy mới nhất khoảng 15 giây |
| `dotnet ef database update --project backend/CareerGap.Api` | Root; credential local | Ba migrations đã áp dụng; API không tự migrate lúc startup |
| `dotnet ef migrations script --idempotent --project backend/CareerGap.Api --output .local/foundation-migrations.sql` | Root | Sinh được SQL idempotent, đã đọc Up/Down và các ràng buộc/trigger liên quan |
| `npm run lint`, `npm run typecheck`, `npm test`, `npm run build` | Frontend; dependency cài bằng lockfile | Thành công; **3 unit tests passed**; build có cảnh báo chunk PDF lớn nhưng đã lazy-load |
| `npm audit --audit-level=high` | Frontend | Lần kiểm tra thành công gần nhất: **0 vulnerabilities**. Đã thay bản typescript-eslint cùng major để xử lý 9 cảnh báo high ban đầu |
| `dotnet list backend/CareerGap.Api/CareerGap.Api.csproj package --vulnerable --include-transitive` | Root; NuGet official sources | Không tìm thấy vulnerable packages tại lần kiểm tra; không phải bảo đảm lâu dài |
| `docker build -t careergap:phase01 .` và `docker compose -f compose.yaml -f compose.smoke.yaml up -d --build api` | Root; Docker Linux | Image build thành công; bản cuối đã gồm validation login null; API/SPA cùng origin chạy local |
| `npm run e2e` với URL 127.0.0.1:5173 | API Windows + Vite proxy, tài khoản/fixture giả | Đợt đầu: **1 passed**, login → private file → fake job → PDF → logout |
| `npm run e2e` với `CAREERGAP_BASE_URL=http://127.0.0.1:9080` | Docker Linux bản cuối, tài khoản/fixture giả | **1 passed**, khoảng 6,7 giây; cùng luồng, no-store, sau logout file 401, viewport 390px không tràn ngang |
| `python scripts/generate-fixtures.py`, `--probe-file ...` | Root; tooling reportlab + font đã có giấy phép | Sinh bộ giả; đọc được PDF hai cột và DOCX có bảng ở cả vi/en; PDF không text trả no_text_ocr_unsupported |
| Poppler render + pypdf extraction, xem PNG | `.local/evidence/` | PDF xuất **3 trang**, có “Nguyễn Ứng Viên”, “quản lý phiên bản”, English text; không có replacement glyph; đã xem đủ 3 trang, không thấy chữ chồng/cắt |
| `git diff --check`, `git check-ignore ...`, `git ls-files .env .local` | Root | Không lỗi whitespace tại lần kiểm tra; .env/.local/test-results bị ignore, không có .env/.local trong tracked files |

19 ca backend kiểm tra: sai quyền giữa hai ứng viên/recruiter/admin/anonymous; CSRF login/logout/upload; null login; idempotency cạnh tranh/conflict; fake output và settlement một lần; lease hết hạn/worker cũ/chi phí unknown; admission ngân sách đồng thời; FK chéo chủ và immutable guards; DOCX Việt/bảng/file lỗi/ZIP quá mức; bốn biến thể JSON/bằng chứng sai; cookie/file qua host mới dùng chung DP database; migration mới/nâng cấp/Down/Up có dữ liệu; hai luồng worker JSON sai/timeout giới hạn retry; account suspended; tránh suy SQL từ PostgreSQL hoặc Java từ JavaScript trong fake extractor.

Các test tạo/drop database **careergap_test_*** riêng và kiểm tra tên DB thực tế; không reset database ứng dụng. Test migration nâng từ Foundation có user mẫu lên bản mới, Down về Foundation rồi Up lại; chưa chạy Down migration đầu tiên về database trống. Dữ liệu local chỉ là dữ liệu giả. Test restart host/lease không chứng minh restore cloud/storage thật hoặc worker chạy liên tục khi hosting sleep.

Bằng chứng local bị Git bỏ qua để không đưa dữ liệu phiên vào repository: TRX ở `tests/CareerGap.Api.Tests/TestResults/foundation.trx`; screenshot desktop/mobile và PDF/render ở `.local/evidence/`. Các bằng chứng này chỉ có trên máy hiện tại, người clone khác cần chạy lại theo README. Không đính kèm cookie, token hoặc thông tin đăng nhập trong report.

## 3. Sửa lỗi và giới hạn còn lại

Đã sửa bài smoke dùng cổng DB nội bộ **5432**, khác cổng host 55432. E2E chọn đúng file ID từ response upload, thay vì nhầm file cũ cùng tên lưu trên filesystem khác. Test xóa giá trị password khỏi DOM ngay sau submit để hạn chế lộ trong failure snapshot. Đã sửa tràn ngang email dài ở mobile và validation login null.

Những phần **chưa kiểm chứng hoặc chưa hoàn thiện**:

- Chưa có public demo/cloud account được xác nhận, private bucket/region thật, HTTPS proxy, production DP certificate/redeploy/backup-restore. Docker smoke dùng HTTP localhost, Testing và file tạm; không thay thế các kiểm tra này.
- Chưa gọi DeepSeek thật, chưa xác nhận model hiện hành/giá/cycle/provider usage; chưa đánh giá độ trễ/chất lượng/chi phí thật. Chưa có user-scope quota hoặc UI đối soát vận hành.
- Search/email mới có interface/fake, chưa có real adapter, truy vấn thật, DNS/sender hoặc gửi thật. Các bước xác minh trước GĐ2 đã ghi trong runbook; không mua/nâng gói.
- Parser có giới hạn và cooperative timeout giữa trang/đoạn, **chưa có hard timeout process**; DOCX chưa phủ header/footer. Chưa có scan thật hoặc corpus đa dạng; không suy chất lượng từ mẫu giả.
- Chưa có tác vụ đối soát/dọn pending object tự động, API thêm version cho CV cũ, revisions/xác nhận GĐ3 hoặc matching sản phẩm. Lát schema/quota/concurrency/đổi kỳ khác thiết kế đầy đủ được ghi rõ trong tài liệu thực thi.
- Quyền/role/owner/UI, CV ẩn liên hệ, nộp lại/block theo tin và lưu/xóa đã được xác nhận trong lượt cập nhật cuối; chưa hiện thực các API/worker GĐ2 tương ứng. Mẫu CV, rubric/quy mô dữ liệu và chi tiết chuyển trạng thái/phân tích lại còn cần hoàn thiện theo giai đoạn; chưa có trao đổi/duyệt giảng viên.
- ESLint 9.39.2 bị npm đánh dấu không còn hỗ trợ; chưa tự nâng major. PDF renderer có chunk khoảng 1,2 MB trước gzip, đã lazy-load; đây là giới hạn prototype cần theo dõi.

Lần trước có thao tác bị ngừng do hệ thống duyệt quyền tự động hết hạn mức, không phải kết luận không an toàn. Lượt tiếp tục đã chạy lại thành công qua cơ chế quyền bình thường; không còn dùng lỗi duyệt quyền đó làm lý do chặn kiểm thử local.

## 4. Usage và ngân sách

- Lời gọi dịch vụ thật do đợt thực thi này thực hiện: **DeepSeek 0, search 0, email 0**. Chưa đọc dashboard/hóa đơn tài khoản; không kết luận chi phí toàn dự án bằng 0.
- Job E2E mới nhất kiểm tra bằng truy vấn tổng hợp DB: provider **Fake**, status **succeeded**, **1 call**, actual **1.00000000 test_credit**. Đây là đơn vị giả để kiểm thử kế toán, không phải USD hoặc credits Tavily.
- DeepSeek mặc định không được bật bằng cấu hình hiện tại; cần model/giá đã xác minh, kỳ provider, secret backend và budget. Không có secret ở frontend/VITE hoặc CI từ đợt triển khai này; không chạy API thật tự động trên mỗi PR.
- Tổng mục tiêu vẫn khoảng 4 USD/tháng cho mọi môi trường/thử nghiệm/demo của một người; kiểm soát local không đo được những lời gọi ngoài ứng dụng. Trước thử thật cần xác nhận phần ngân sách tháng còn lại.

## 5. Đối chiếu điều kiện M1 cũ — lưu lịch sử

Bảng này phản ánh tiêu chí trước quyết định dừng deploy; không dùng thiếu cloud làm điều kiện chặn M1 localhost mới. Xem mục 9 cho trạng thái hiện tại.

| Điều kiện theo roadmap | Trạng thái | Bằng chứng còn thiếu |
|---|---|---|
| Người thực hiện dựng lại theo README, CI build và demo truy cập được | **Chưa đạt** | CI bdb3aeb đã đạt; còn kiểm chứng dựng lại theo README và demo public, không yêu cầu nhiều người |
| Luồng triển khai login → private file → AI thật, xử lý lỗi liên quan | **Chưa đạt** | Local proxy/Linux fake E2E đạt; thiếu AI thật trên cloud và storage thật |
| Hạn mức lời gọi thật, usage, cách ngừng chi phí và bảo vệ khóa | **Chưa kiểm chứng** | Code/quota fake có test; chưa đo provider thật, kỳ tính phí và đối soát dashboard |
| Quyết định quyền ưu tiên cần GĐ2, phần chưa chốt bị khóa rõ | **Đạt ở mức yêu cầu** | Người dùng đã chốt quyền/UI/role/owner/CV ẩn/nộp lại-block theo tin/lưu-xóa 90/7/14; prototype vẫn self-only, không mô tả API tương lai là đã có; chi tiết chuyển trạng thái còn lại hoàn thiện trước API phụ thuộc |
| Dữ liệu có nguồn/định dạng/ngôn ngữ, kế hoạch nhãn và trao đổi giảng viên | **Chưa đạt** | Manifest giả và kế hoạch có; cần hoàn thiện kiểm kê CV/JD mẫu/kế hoạch dữ liệu đại diện và trao đổi giảng viên. Không yêu cầu hoàn thành corpus thật hoặc gán nhãn cuối kỳ ngay GĐ1 |
| Kết luận/phương án cho hosting/storage/parser/PDF/search/email | **Chưa đạt** | Docker/parser/PDF có bằng chứng; cloud storage/search/email còn cần tài khoản và thử thật theo runbook |

**Chưa nghiệm thu M1, chưa đánh dấu toàn GĐ1 hoàn thành.** G1-12 hoàn thành nghĩa là đã lập báo cáo đợt thực thi, không phải đủ điều kiện chuyển toàn bộ sang GĐ2.

## 6. Việc tiếp theo tại thời điểm báo cáo cũ — đã thay thế

Danh sách dưới được lưu lịch sử; không tiếp tục các bước deploy/cloud. Thứ tự hiện tại ở mục 9.

1. Chủ dự án tự review thay đổi local (compose smoke, validation login, test và tài liệu), sau đó commit/publish; xác minh CI mới đúng commit, không tái dùng chứng nhận CI bdb3aeb cho mã chưa push.
2. Chủ dự án trực tiếp quản lý deploy, xác nhận quyền tài khoản hosting/database/private storage, cert và DNS/sender. Chọn hosting theo kết quả tích hợp; image React+.NET là phương án đã thử local, chưa phê duyệt cloud.
3. Bố trí secrets ở backend/secret store, quyền runtime/migration tách biệt; chạy luồng HTTPS/private file/DP/restart/restore và một DeepSeek thật trong ngân sách được phép, ghi usage/kết quả thực.
4. Hiện thực các quyết định quyền/GĐ2 đã chốt với migration/test; không hỏi lại chính sách. Thiết kế bản dẫn xuất ẩn liên hệ, block theo tin và purge/restore cần được kiểm chứng trước mở API tương ứng.
5. Hoàn thiện real search/email adapters và kiểm chứng một truy vấn kỹ năng không PII, sender/gửi thật; bổ sung parser hard timeout/corpus khó trước mở upload không tin cậy trên Internet.
6. Thu thập CV/JD hợp lệ, thống nhất lịch/nhãn/rubric với giảng viên và ghi bằng chứng người thực hiện dựng lại dự án theo README. Cập nhật task/report bằng bằng chứng rồi nghiệm thu lại từng điều kiện M1.

Sau kiểm thử đã dừng API smoke và xóa container thử cũ do phiên này tạo; giữ PostgreSQL local/volume và dữ liệu giả để tiếp tục. Không xóa/reset database dùng chung, không triển khai cloud hoặc phát sinh gói dịch vụ trả phí.

## 7. Rà soát theo yêu cầu chuyển sang dự án cá nhân

Đã cập nhật AGENTS.md, README, bốn tài liệu nguồn, task, tài liệu triển khai và kế hoạch đánh giá: một người đảm nhiệm toàn bộ công việc; không phân công A/B/C, không bắt buộc người khác review/ba thành viên chạy thử, không dự trù lịch dựa trên nhiều người phát triển đồng thời. Ngân sách tổng và phạm vi chức năng cốt lõi giữ nguyên. Mục tiêu 12 tuần cần rà theo số giờ thực tế; chưa coi việc giảm nhân sự là bằng chứng lịch vẫn khả thi.

Các tham chiếu nhóm dữ liệu, nhóm kỹ năng/tiêu chí và cộng tác recruiter trong sản phẩm được giữ đúng nghĩa; chúng không mô tả nhân sự phát triển. Domain được nêu trong tài liệu vẫn là giả định quyền sở hữu, không phải lý do suy ra làm nhóm.

Đã kiểm tra lại GitHub: remote vẫn ở bdb3aeb, CI run 38718678231 success. Tài liệu đã có [danh sách đóng việc G1-R01–R08](../tasks/phase-01-foundation.md#chốt-danh-sách-còn-lại-của-gđ1-cho-một-người), với chủ dự án là người phụ trách duy nhất, đầu vào/phụ thuộc và bằng chứng nghiệm thu cho từng mục. Danh sách này thay cho các ghi chú tồn đọng chung chung; không gộp việc hiện thực sản phẩm của GĐ2–5 thành điều kiện mới cho GĐ1.

Người dùng đã trả lời và các quyết định được cập nhật ở mục 8. Dịch vụ sẽ cung cấp sau qua checklist; không mặc định chưa có tài khoản. M1 vẫn **chưa đạt**: đã đóng phần quyết định ưu tiên và bỏ điều kiện nhân sự không còn áp dụng, nhưng chưa có bằng chứng cloud/AI thật hoặc đủ đầu vào đánh giá.

## 8. Chốt chính sách và đầu vào dịch vụ theo phản hồi cuối

- Đã chốt UI tiếng Việt, CV/JD Việt–Anh, khách chỉ JD, CV yêu cầu đăng nhập; admin không mặc định đọc private; đăng ký một role Candidate/Recruiter, mỗi công ty một recruiter owner.
- Ứng viên khác chỉ nhận bản CV ẩn liên hệ. Nộp lại sau rút/bị loại được phép nếu không có đơn hiệu lực và không bị block ở tin đó; block chỉ đúng tin, không ảnh hưởng tin khác. Recruiter còn đọc snapshot đơn rút trong hạn.
- Người dùng chọn **90 ngày / 7 ngày / 14 ngày**, đã ghi thành [chính sách lưu/xóa](../phase-01-data-retention.md), không còn là đề xuất chờ duyệt. Xóa tài khoản thu hồi truy cập ngay và ưu tiên hơn thời hạn đọc đơn.
- Đã tạo [checklist S01–S12](../phase-01-services-checklist.md) có cột trạng thái/thông tin an toàn/điều kiện kiểm chứng và mẫu điền. Ngoài GitHub đã kiểm chứng, các dịch vụ khác ghi Chưa cung cấp đúng phản hồi người dùng; chưa tạo/mua hoặc thử bằng credentials thật.
- Đã cập nhật đặc tả, hướng dẫn agent, công nghệ, roadmap, thiết kế database và task. G1-02 đóng ở mức xác nhận yêu cầu; metadata file ẩn, job_candidate_blocks và retention/purge trong database vẫn là thiết kế GĐ2, chưa phải migration đã áp dụng.
- Đợt này chỉ sửa tài liệu, kiểm tra Markdown/liên kết/tính nhất quán; không chạy lại API thật, không ghi các ca kiểm thử chính sách mới thành pass. Các bằng chứng code/test cũ ở phần 2 được giữ nguyên.

Kiểm tra đợt tài liệu: rà các câu về nhân sự/phân công/review/ngân sách, liên kết Markdown và whitespace; không có thay đổi mã nguồn mới trong đợt này. Các thay đổi code đang ở working tree là từ đợt trước và được giữ nguyên. Không chạy lại toàn bộ test/DeepSeek chỉ vì sửa tài liệu; 19/3/E2E ở phần 2 là kết quả lần chạy trước, không phải kiểm thử mới trong lần rà soát nhân sự.

## 9. Quyết định dừng deploy, ưu tiên localhost — 15/10/2026

Theo yêu cầu mới của người dùng, dừng toàn bộ công việc deploy và hoàn thiện sản phẩm trên localhost trước. Roadmap chuyển M1 sang nền tảng local với job fake; kiểm chứng hosting/production được hoãn, chỉ mở lại sau khi sản phẩm hoàn thiện local và người dùng yêu cầu. M1 mới **chưa nghiệm thu đầy đủ**: vẫn cần bằng chứng dựng lại môi trường, đối chiếu các khoảng trống local và kế hoạch dữ liệu; thay tiêu chí không tự làm mốc đạt.

Thứ tự tiếp theo: dựng/kiểm tra localhost → hoàn thiện nghiệp vụ GĐ2 → các luồng CV/AI/nguồn học GĐ3–5 → đánh giá và bàn giao local. Xem [task hiện tại](../tasks/phase-01-foundation.md#ưu-tiên-hiện-tại--localhost). Tích hợp provider thật được theo dõi riêng, không dùng fake để công nhận chất lượng hoặc giao nhận email thật.

Checklist hiện ghi Neon S04 đã cấu hình; các câu Chưa cung cấp ở báo cáo cũ phản ánh thời điểm trước cập nhật đó. Đợt sửa tài liệu này không kết nối lại hoặc sửa Neon, không xác minh lại cloud, không xóa dịch vụ/dữ liệu và không thực hiện deploy. Workflow trong repository chỉ build/test frontend/backend/Docker, không có bước deploy; giữ nguyên CI.

Đợt này chỉ cập nhật tài liệu, rà nội dung/liên kết và whitespace. Không chạy lại build/test sản phẩm hoặc API thật; mọi số liệu test/CI ở phần trước là bằng chứng lịch sử.
