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

## 10. Lặp lại L01 trên localhost — 16/10/2026

Lượt này bắt đầu thực hiện [kế hoạch L01–L09](../tasks/next-local-development-plan.md). Trạng thái Git lúc bắt đầu: `main`, `HEAD=5f4defe`, working tree sạch, `origin/main` cùng commit. Đã sửa các liên kết Markdown trỏ `docs/` cũ sang `project-docs/`; kiểm tra 14 tệp Markdown không còn broken local link. Không thay đổi database Neon/cloud.

| Kiểm tra | Thư mục/điều kiện | Kết quả 16/10 |
|---|---|---|
| Toolchain | Root; .NET 10.0.401, Node v24.16.0, npm 11.13.0, Docker Linux; `.env` trỏ `localhost:55432/careergap`; PostgreSQL container 16.10 healthy | Đạt; connection string không được in. Container có cảnh báo orphan `careergapai-api-1`; không dừng/đổi container đó |
| Restore/migration | Root; `. ./scripts/dev-env.ps1`, `docker compose up -d db`, `dotnet tool restore`, restore locked; database ứng dụng local đã có schema hiện hành | Tool/restore đạt; `dotnet ef database update --project backend/CareerGap.Api` báo database đã up to date, không có migration mới áp dụng |
| Backend/frontend | Root `./scripts/test.ps1`; test DB ngẫu nhiên `careergap_test_*` | **19 backend passed**, **3 frontend passed**; lint, typecheck, build đạt. Build còn cảnh báo chunk PDF khoảng 1.2 MB |
| Browser E2E | Database cô lập mới tạo `careergap_test_l01_*`, migrations, tài khoản giả ngẫu nhiên, DOCX tổng hợp; API 5080 + Vite 5173 | **1 passed**: login → upload và đọc file private → fake job → PDF → logout; sau logout file trả 401. Sau test đã dừng API/Vite và drop đúng database test. Không đụng DB ứng dụng |
| Docker | Root `docker build -t careergap:l01-5f4defe .` | Image build thành công; dùng tag mới để tránh ghi đè tag hiện có |
| CI | GitHub Actions của SHA `5f4defe` | Run [`38774073395`](https://github.com/LeHoangLich/CareerGapAI/actions/runs/38774073395) hoàn tất success cho frontend/backend/Docker; không lấy run cũ của SHA khác thay thế |
| Liên kết tài liệu | README, AGENTS và toàn bộ Markdown trong `project-docs/` | 14 file được quét; 0 liên kết nội bộ hỏng |

Các lệnh test/migration dùng cấu hình local, test tự tạo database riêng. E2E dùng dữ liệu giả, không gọi dịch vụ thật hoặc tạo hóa đơn. Tài khoản chỉ tồn tại trong database test đã drop; file được upload vào thư mục riêng `.local/l01-e2e-files`, ngoài Git. `.local` bị ignore. Lượt này không kiểm thử clean clone/máy thứ hai.

**L01 local execution: đạt các bước build/test/reproducibility được chạy. M1 tổng thể: vẫn chưa nghiệm thu.** Chưa có bằng chứng người thực hiện đã trao đổi/đồng thuận với giảng viên về quy mô, nhãn, rubric và lịch đánh giá; chưa có corpus CV/JD được phép dùng ngoài bộ tổng hợp smoke. Đây không chặn L02–L03; cần cập nhật khi người thực hiện có kết quả trao đổi/dữ liệu hợp lệ. Chưa chạy API DeepSeek/search/email thật hoặc thao tác deploy.

## 11. L02 trên localhost — 16/10/2026

Đã triển khai đăng ký một role Candidate/Recruiter (Admin không tự đăng ký), tạo candidate profile cùng transaction, đổi/khôi phục mật khẩu qua Identity token và fake mailbox local, profile chính chủ với allowlist trường chia sẻ, DTO recruiter đã lọc, cùng optimistic concurrency `row_version`. UI có chọn role, hồ sơ/chia sẻ, recruiter profile lookup và luồng đổi/khôi phục mật khẩu. Không có email thật; thay đổi chưa được publish và không có CI mới.

| Kiểm tra | Thư mục/điều kiện | Kết quả 16/10 |
|---|---|---|
| Migration/schema | Root; database ứng dụng PostgreSQL local và database E2E riêng có tiền tố `careergap_test_l02_` | Hai migration profile đã áp dụng; kiểm tra được `__EFMigrationsHistory`. Database E2E được drop sau kiểm thử; không reset database ứng dụng |
| Backend/frontend | Root `./scripts/test.ps1`; test DB PostgreSQL ngẫu nhiên | **22 backend passed**, **3 frontend passed**; lint, typecheck, build đạt. Build giữ cảnh báo chunk PDF khoảng 1,2 MB |
| Browser E2E | API `127.0.0.1:5080`, Vite `127.0.0.1:5173`, database giả riêng | `npx playwright test e2e/accounts.spec.ts`: **1 passed**, đăng ký candidate → chia sẻ profile → đổi mật khẩu → logout → fake recovery → reset → đăng nhập lại |
| Docker build | Root `docker build -t careergap:l02-local-20261016 .` | Thành công; chỉ xác nhận đóng gói/build, không phải Docker runtime smoke |
| Phạm vi lời gọi | Fake mailbox, dữ liệu `example.invalid`, PostgreSQL local | Không gọi provider thật hoặc gửi email; token hết hạn được kiểm tra bằng lifespan 5 giây chỉ trong environment Testing |

L02 được xác minh local theo các ca hiện có, không xác nhận chất lượng delivery email thật, bảo mật vận hành Internet hay CI của thay đổi chưa publish. Tại thời điểm viết mục này, bước tiếp theo là L03; trạng thái sau khi thực hiện xem mục 12.

## 12. L03 — công ty và JD công khai trên localhost — 16/10/2026

L03 thêm hồ sơ công ty một recruiter owner, tạo/sửa công ty có row version, tạo JD theo version bất biến, draft/publish/close, trang tìm kiếm JD công khai có bộ lọc và phân trang, cùng saved jobs theo candidate. API lọc theo owner ở backend; guest chỉ tìm tin published, tin nháp không lộ. Version JD được khóa bằng PostgreSQL trigger; company owner và job company ID bất biến. Deadline được kiểm tra khi publish và `acceptingApplications` được tính bằng thời gian server. Ứng tuyển chưa mở.

| Kiểm tra | Thư mục/điều kiện | Kết quả 16/10 |
|---|---|---|
| Migration/schema | Root; PostgreSQL local 16.10, đã có profile migrations L02 | `RecruitmentJobsLocal` được review cả `Up`/`Down`, áp dụng bằng `dotnet ef database update --project backend/CareerGap.Api`; integration tests dùng PostgreSQL thử ngẫu nhiên, kiểm tra upgrade/down/upgrade. Không reset database ứng dụng |
| Backend/frontend | Root `./scripts/test.ps1` | **23 backend passed**, **3 frontend passed**; lint, typecheck, Vitest và build đạt. Build còn cảnh báo chunk PDF khoảng 1,2 MB |
| Browser E2E | API `127.0.0.1:5080`, Vite `127.0.0.1:5173`, local app database | `npx playwright test e2e/accounts.spec.ts e2e/jobs.spec.ts`: **2 passed**. E2E L03 tạo công ty/JD nháp, xác nhận tìm kiếm công khai ẩn draft, publish thì hiện, sửa tạo version 2, đóng tin thì mất khỏi kết quả. Các tài khoản/công ty/JD giả được giữ trong local app database; không drop hoặc reset database |
| Docker build | Root `docker build -t careergap:l03-local-20261016 .` | Build image thành công; không phải runtime smoke |
| Lời gọi và dữ liệu | Fake/local, email `example.invalid` | Không gọi provider, email hay dịch vụ cloud thật; mọi bản ghi E2E là dữ liệu tổng hợp |

E2E phát hiện và sửa một lỗi nhập liệu: chuỗi rỗng từ trường website tùy chọn nay được chuẩn hóa thành null trước validation. Test API có kiểm tra hồi quy. L03 đã đạt trên localhost theo kiểm tra đã chạy; working tree sau commit `5f4defe` chưa được CI xác nhận hoặc publish. M1 tổng thể vẫn chưa nghiệm thu do còn thiếu bằng chứng dữ liệu/trao đổi đánh giá. Tại thời điểm ghi mục 12, bước kế tiếp là L04 quản lý CV theo version.

## 13. L04 — CV có tên và version file riêng tư — 16/10/2026

L04 cho phép candidate tạo CV, đổi tên và thêm một file mới làm version tiếp theo. Mỗi version giữ `file_id`, SHA-256 và số version riêng; không ghi đè file cũ. Candidate có thể chọn version cụ thể trong giao diện; quyền tải vẫn chỉ chính chủ. Thêm version khóa hàng Resume cha trong transaction ngắn, cấp số sau khóa và tăng row version; database trigger chặn đổi owner/sửa/xóa version, đồng thời chỉ nhận version tham chiếu file `ready` đã xác minh đúng hash. Parser revision, CV dựng từ mẫu và ứng tuyển chưa mở.

| Kiểm tra | Thư mục/điều kiện | Kết quả 16/10 |
|---|---|---|
| Migration/schema | Root; PostgreSQL local 16.10; test DB ngẫu nhiên PostgreSQL | `ResumeCollectionVersioning` thêm row version/updated_at và guard; integration suite kiểm tra migration mới/up/down trên DB thử. Migration đã áp dụng trên app DB local; không reset dữ liệu |
| Backend/frontend | Root `./scripts/test.ps1` | **24 backend passed**, **3 frontend passed**; lint, typecheck, Vitest và build đạt. Build vẫn có cảnh báo chunk PDF khoảng 1,2 MB |
| Browser E2E | API `127.0.0.1:5080`, Vite `127.0.0.1:5173`, DOCX giả ở `.local/fixtures/synthetic-vi.docx` | Ba bài `accounts.spec.ts`, `jobs.spec.ts`, `resumes.spec.ts`: **3 passed**. L04 kiểm tra tạo CV, thêm version 2, chọn version 1/2, đổi tên và giữ đủ hai link file private |
| Migration và concurrency | Resume có version ban đầu; hai upload chạy đồng thời vào cùng Resume | Hai request nhận version 2 và 3 khác nhau. Test kiểm tra owner, row_version cũ trả conflict, version bất biến, file pending/hash sai bị trigger từ chối; CV/file không đọc được bởi candidate khác hoặc recruiter |
| Docker | Root `docker build -t careergap:l04-local-20261016 .` | Build image thành công; chưa chạy container runtime smoke |
| Dữ liệu và tích hợp | API Fake, local storage, tài khoản `example.invalid` | Không gọi provider/email/cloud thật. Các tài khoản, company/job và CV giả do E2E tạo được giữ trong local app database; không drop hoặc reset DB |

L04 đạt trên localhost theo các kiểm tra đã chạy. Working tree sau commit `5f4defe` chưa được CI xác nhận/publish. M1 tổng thể chưa nghiệm thu do còn thiếu bằng chứng dữ liệu/trao đổi đánh giá. L05 được ghi tại mục 14.

## 14. L05 — ứng tuyển và snapshot CV/JD — 16/10/2026

Candidate có thể nộp đúng một CV version vào job đang nhận đơn, dù CV vẫn private. API khóa hàng `jobs` trong transaction, kiểm tra trạng thái/deadline/current JD version, candidate owner và file đã xác minh; cùng `submissionKey`/hash trả application cũ, key trùng nhưng input khác trả conflict. Partial unique index ngăn hai application đang hiệu lực cho một candidate/job. Việc sửa hoặc đóng job phối hợp cùng hàng khóa; application ghim CV/JD version trước khi ghi.

Tạo application, history đầu tiên (`null → submitted`) và hai notification (candidate/recruiter owner) trong cùng transaction. PostgreSQL trigger giữ các cột snapshot bất biến và history append-only. Candidate xem danh sách đơn/thông báo và file version đã gửi. Recruiter chỉ xem application của job thuộc công ty mình; tải CV qua application route kiểm tra lại owner/status/retention, còn `/api/files/{fileId}` không cấp quyền cho recruiter. Đơn submitted/reviewing/shortlisted còn đọc; rejected/withdrawn bị ẩn và chặn tải sau 90 ngày. Candidate chính chủ vẫn xem snapshot của mình. Chưa có mutation đổi trạng thái, ghi chú, block/nộp lại hoặc revision ID.

| Kiểm tra | Thư mục/điều kiện | Kết quả 16/10 |
|---|---|---|
| Migration/schema | Root; PostgreSQL local 16.10 và DB test ngẫu nhiên | `ApplicationSnapshotsAndNotifications` tạo applications/history/notifications, composite FKs, partial unique active, submission key unique và trigger bảo vệ snapshot/history. Đã review `Up`/`Down`, áp dụng trên app DB local; không reset DB. |
| Backend | Root `./scripts/test.ps1`; PostgreSQL test DB tạo/drop riêng | **26 backend passed**. Ca L05 kiểm tra Candidate/Recruiter role, CV version không thuộc user, replay và hash conflict, active duplicate, JD edit snapshot, owner scoping, direct file ID, tải qua application, notification/read, snapshot/history trigger, retention 90 ngày và tranh chấp apply/close. |
| Frontend | Root `./scripts/test.ps1` hoặc `frontend/` lint/typecheck/test/build | **3 frontend passed**; ESLint, TypeScript, Vitest và Vite build đạt. Cảnh báo chunk PDF khoảng 1,2 MB còn như trước. |
| Browser E2E | API `127.0.0.1:5080` environment Testing, Vite `127.0.0.1:5173`, DOCX giả | `accounts.spec.ts`, `jobs.spec.ts`, `resumes.spec.ts`, `applications.spec.ts`: **4 passed**. E2E L05 Candidate đăng nhập, nộp CV, thấy application/notification; recruiter chỉ tải được file từ đúng application, file ID trực tiếp trả 404. Dữ liệu giả được giữ trong local app DB. |
| Docker | Root `docker build -t careergap:l05-local-20261016 .` | Build image local thành công; đây là build, không phải runtime smoke/CI. |
| Provider/CI | Fake LLM, local storage, email `example.invalid`; branch hiện tại | Không gọi provider/email/cloud thật. CI run `38774073395` vẫn chỉ chứng nhận commit `5f4defe`; L02–L05 working tree chưa được push hoặc CI xác nhận. M1 chưa nghiệm thu do thiếu bằng chứng dữ liệu/trao đổi đánh giá. |

L05 hoàn tất phần application/snapshot theo lát localhost, không chờ AI. Pin JD version là lựa chọn kỹ thuật theo thiết kế 04, chưa phải quyết định nghiệp vụ riêng; CV/JD revisions còn chưa có. Trạng thái L06 và phần còn lại được ghi tại mục 15.

## 15. L06 — sàng lọc, ghi chú và block theo tin — 16/10/2026

Người dùng xác nhận ma trận recruiter owner: `submitted → reviewing/shortlisted/rejected`; `reviewing → shortlisted/rejected`; `shortlisted → reviewing/rejected`. Candidate có thể rút mọi đơn còn hiệu lực; `rejected` và `withdrawn` kết thúc đơn, nộp lại tạo application/snapshot mới nếu không còn đơn hiệu lực và không bị block ở chính tin đó.

Backend thêm `PUT /api/applications/{id}/status`, `POST /api/applications/{id}/withdraw`, history, ghi chú nội bộ và block/bỏ block theo job. Status API khóa application, kiểm tra actor/transition/`row_version`; application, history và notification được ghi cùng transaction. Candidate rút đơn thì recruiter owner được báo, còn chuyển trạng thái từ recruiter thì candidate được báo. Ghi chú append-only, giới hạn 4.000 ký tự, chỉ recruiter owner đọc trong cửa sổ retention; không có trong DTO/thông báo candidate. Bảng block có khóa `(job_id,candidate_id)`; block và apply khóa cùng hàng job, block không sửa hay thu hồi quyền đọc application đã nộp. Block chỉ chặn lần nộp sau ở tin đó; bỏ chặn cho phép reapply bằng key/application mới.

UI recruiter có nút chuyển trạng thái hợp lệ theo từng trạng thái, ghi chú riêng và chặn/bỏ chặn; UI candidate xem history và rút đơn còn hiệu lực. History không lộ danh tính actor hoặc note. Candidate thấy application cũ đã kết thúc và application mới là hai đơn riêng.

| Kiểm tra | Thư mục/điều kiện | Kết quả 16/10 |
|---|---|---|
| Migration/schema | Root; PostgreSQL local 16.10; test DB ngẫu nhiên | `ApplicationNotesAndJobCandidateBlocks` thêm bảng notes/block, FK Restrict, index và trigger note append-only; đã review `Up`/`Down`, áp dụng trên database local. Tổng 9 migrations; 6 migration feature L02–L06. `dotnet ef migrations has-pending-model-changes` báo không có thay đổi model sau migration. |
| Backend | Root `./scripts/test.ps1`; test tạo/drop `careergap_test_*` riêng | **29 backend passed**. Ca L06 kiểm tra actor/owner, từng nhánh chuyển hợp lệ, stale row version, terminal state, history, notification, note visibility/append-only, block idempotent/scope, snapshot còn đọc, block theo một tin, reapply sau bỏ chặn và race block/apply. |
| Frontend | Root `./scripts/test.ps1` | **3 frontend passed**; ESLint, TypeScript, Vitest và Vite build đạt. Build còn cảnh báo chunk PDF xấp xỉ 1,2 MB. |
| Browser E2E | API 5080 environment Testing, Vite 5173, DOCX giả, local app DB | Bốn workflow `accounts.spec.ts`, `applications.spec.ts`, `jobs.spec.ts`, `resumes.spec.ts`: **4 passed**. Application E2E kiểm tra status transition, note private, block, history, candidate withdraw, blocked reapply và recruiter đọc snapshot application đã rút. Dữ liệu tổng hợp được giữ trong local app DB. `foundation.spec.ts` legacy riêng cần `DEMO_EMAIL`/`DEMO_PASSWORD`; hai biến không có trong lượt kiểm tra nên spec đó không chạy thành công. |
| Docker | Root `docker build -t careergap:l06-local-20261016 .` | Build image thành công; không phải Docker runtime smoke, CI hoặc deploy. |
| Provider/CI | Fake/local, email `example.invalid`; working tree sau `5f4defe` | Không gọi provider/email/cloud thật. CI run `38774073395` chỉ chứng nhận commit `5f4defe`; L02–L06 chưa được CI xác nhận hoặc publish. M1 vẫn chưa nghiệm thu do thiếu bằng chứng dữ liệu/trao đổi đánh giá. |

L06 hoàn tất trên localhost theo quyết định chuyển trạng thái đã xác nhận. Candidate có thể xem lịch sử và snapshot của mình; recruiter owner chỉ xem đúng ứng viên/tin, ghi note nội bộ và quản lý block của tin. Không có AI auto-reject hoặc thay đổi trạng thái. L07 — CV công khai và bản dẫn xuất ẩn liên hệ — là lát kế tiếp. M1 tổng thể chưa nghiệm thu; không deploy, không push và không gọi dịch vụ thật trong lượt này.

## 16. L07 — CV công khai và bản dẫn xuất ẩn liên hệ — 16/10/2026

Candidate chủ động chọn một `resumeVersionId` đang sở hữu để public; upload CV/version mới không đổi lựa chọn. Publish/replace có `expectedRowVersion`, khóa candidate profile trong transaction và chỉ gắn lựa chọn sau khi kiểm tra file nguồn/hash. Hide cũng kiểm tra row version và xóa selection. URL file gắn cả candidate và version; sau replace/hide, URL version cũ trả 404. Không đổi file gốc hoặc snapshot application.

`candidate_public_resumes` ghim `resume_version_id`/`source_hash`, có `ready`/`not_ready`, `redacted_file_id`, `redaction_method_version`, `redaction_verified_at`, `published_at` và `row_version`; composite FK bảo đảm owner/version/file cùng candidate. Lựa chọn public có tối đa một hàng cho mỗi candidate. API list chỉ hiện derivative `ready` cho candidate khác; recruiter đọc file gốc qua public context, còn hồ sơ ứng tuyển vẫn dùng route snapshot owner-scoped. Profile name/headline chỉ được trả khi field tương ứng đã bật shared visibility. Guest/admin không có đường đọc CV công khai.

Bản dẫn xuất hiện được dựng từ các đoạn text do parser PDF/DOCX đọc được: email, URL và mẫu số điện thoại nhận diện được bị thay; DOCX mới không chép ảnh, hyperlink, metadata, QR hoặc các thành phần package khác từ nguồn. Builder parse/scan lại kết quả; API kiểm tra object hash và source hash trước khi trả candidate khác. Nếu việc dựng hoặc kiểm chứng không sẵn sàng, selection ở trạng thái `not_ready`; candidate khác nhận lỗi trạng thái, tuyệt đối không fallback file gốc, còn recruiter tiếp tục được đọc file public gốc. Cách này chưa hỗ trợ OCR và chưa chứng minh nhận diện mọi biến thể contact/obfuscation; cần corpus kiểm thử trước khi tuyên bố độ phủ tổng quát.

| Kiểm tra | Thư mục/điều kiện | Kết quả 16/10 |
|---|---|---|
| Migration/schema | Root; PostgreSQL local 16.10 và DB kiểm thử riêng | `PublicResumeRedaction` thêm bảng và FK ghép owner/version/file; migration áp dụng local và integration DB mới. Tổng 10 migrations (3 nền tảng + 7 feature). `dotnet ef migrations has-pending-model-changes` báo không có model diff. |
| Backend | Root `./scripts/test.ps1`; test tạo/drop `careergap_test_*` riêng | **33 backend passed**. Test L07 gồm DOCX có image/hyperlink/metadata và PDF có contact/metadata/hyperlink, source hash, recruiter file gốc, candidate derivative không còn email/phone/URL, ZIP chỉ có document text parts, `not_ready` no-fallback, role/direct-id checks, cạnh tranh publish cùng expected state, upload version mới không tự public, replace/hide chặn URL version cũ. |
| Frontend | Root `./scripts/test.ps1` | **3 frontend passed**; ESLint, TypeScript, Vitest và Vite build đạt. Build vẫn có cảnh báo chunk PDF khoảng 1,2 MB. |
| Browser E2E | API 5080 environment Testing, Vite 5173, DOCX giả, local app DB | Bốn workflow `accounts.spec.ts`, `applications.spec.ts`, `jobs.spec.ts`, `resumes.spec.ts`: **4 passed**; resume workflow publish rồi hide CV public. Dữ liệu E2E tổng hợp được giữ trong local app DB. Legacy `foundation.spec.ts` cần `DEMO_EMAIL`/`DEMO_PASSWORD`; không chạy trong lượt này. |
| Docker | Root `docker build -t careergap:l07-local-20261016 .` | Image local build thành công; đây là build, không phải runtime smoke, CI hoặc deploy. |
| Provider/CI | Fake/local storage, email `example.invalid`; working tree sau `5f4defe` | Không gọi DeepSeek/search/email/cloud thật, không push. CI run `38774073395` chỉ chứng nhận commit `5f4defe`; L02–L07 chưa được CI xác nhận. M1 vẫn chưa nghiệm thu do thiếu bằng chứng dữ liệu/trao đổi đánh giá. |

L07 hoàn tất theo lát localhost và acceptance test đã ghi ở kế hoạch. Public CV browsing mới ở mức danh sách phân trang và mở file; search/ranking baseline thuộc L09, revision CV và OCR thuộc các lát sau. Tiếp theo là L08: deletion request, purge, retention và restore theo chính sách đã chốt. Không deploy và không coi local E2E hoặc Docker build là bằng chứng provider thật.

## 17. L08 — xóa tài khoản, retention đơn và cleanup file local (16/10/2026)

L08 hiện thực trên localhost chính sách 90/7/14 ngày. Candidate yêu cầu xóa sau xác minh password: middleware thu hồi quyền API ngay, thu hồi public CV và ngừng dùng các job AI; phiên còn lại chỉ được đi vào recovery/cancel/logout. Trước 7 ngày candidate có thể hủy bằng password; hủy không tự public lại CV hoặc mở tin recruiter đã bị archive. Worker purge xử lý dữ liệu Candidate hoặc Recruiter theo đúng chủ sở hữu, lưu object deletion ledger và retry lỗi storage; purge recruiter giữ application/history/note cần thiết của candidate khác, ẩn danh công ty và vô hiệu hóa quyền owner. Usage attempt còn chờ settlement được giữ tối thiểu theo thiết kế. Admin không có API đọc CV riêng.

Terminal application `rejected`/`withdrawn` bị ẩn qua cả hai ngữ cảnh từ `status_changed_at + 90 ngày`, kể cả khi worker chưa chạy; worker xóa notification, note, history và application theo batch. CV/version/file thuộc candidate không bị xóa do một đơn hết hạn. File upload `pending` cũ hơn 24 giờ được đổi thành `delete_pending`, chọn bằng row lock/`SKIP LOCKED`; upload hoàn tất phải khóa và xác nhận trạng thái còn pending trước khi đặt file ready. Mọi upload CV gốc và DOCX dẫn xuất tạo row metadata trước object PUT để lỗi hoặc process stop không để object mới thành file vô danh trong luồng bình thường. Worker bỏ qua file còn tham chiếu, xóa object ngoài transaction DB, xóa row sau khi xác minh tham chiếu lần nữa, và backoff khi provider trả lỗi. File `failed` không tự dọn vì retention riêng chưa được chốt; chưa có sweep/list object để phát hiện object không có metadata do dữ liệu cũ hoặc thao tác ngoài ứng dụng.

| Kiểm tra | Điều kiện | Kết quả 16/10 |
|---|---|---|
| PostgreSQL migrations | Root, local PostgreSQL 16.10; backup app DB trước migration | `AccountDeletionLifecycle`, `ApplicationRetentionWindow`, `OrphanFileCleanupLifecycle` đã áp dụng; tổng **13 migrations** (3 nền + 10 feature). `dotnet ef migrations has-pending-model-changes --project backend/CareerGap.Api` báo không có model diff. Migration cleanup cho phép trạng thái `delete_pending`, retry fields/index; `Down` từ chối nếu còn state cleanup cần giữ. |
| Backend/frontend | Root `./scripts/test.ps1`; test chỉ tạo/drop `careergap_test_*` riêng | **40 backend passed**, **3 frontend passed**, ESLint/typecheck/Vitest/Vite build đạt. Gồm T0 revocation/cancel không republish, purge candidate/recruiter không xóa dữ liệu người khác, retry object deletion, terminal read cutoff đúng 90 ngày, pending upload grace/retry và CV sở hữu còn nguyên. Build có cảnh báo chunk PDF khoảng 1,2 MB. |
| Restore cô lập | Backup DB + Local files + Data Protection key; API không chạy | Backup `.local/backups/20261015T201258127Z-f97c354778b44740811ffcb992659297`; restore thành `careergap_restore_05af1341e2ed405a89c44b81c963f784`, tự áp migration mới, replay **5 deletion ledger entries**, kiểm tra **1 DP key / 38 objects**. API không được khởi động. Backup/restore từ snapshot cũ và ledger hiện hành đã được kiểm chứng. |
| Script backup/expire | PowerShell local, Development, storage Local | Parser PowerShell toàn bộ `scripts/*.ps1` đạt; backup chạy thành công trước migration. Expiry script đã được smoke test trước đó với thời gian giả; lần này báo chưa có hạn đã qua. Expiry cần chạy tay, áp dụng bảo thủ trên gói backup local; chưa có scheduler, offsite, S3/DP certificate backup hoặc chứng minh đảm bảo production 14 ngày. |
| Container | Root `docker build -t careergap:l08-local-20261016 .` | Docker image build thành công; cảnh báo chunk PDF đã biết. Đây không phải runtime smoke/CI/deploy. |

Bốn browser workflow E2E đã đạt trước lần refactor metadata-first cuối của luồng file; sau refactor `./scripts/test.ps1` đã chạy toàn bộ PostgreSQL integration suite nhưng chưa chạy lại browser E2E. CI chỉ chứng nhận commit `5f4defe` (run `38774073395`); working tree L08 chưa được push hoặc CI xác nhận. Chưa gọi provider thật. Hạn chế vận hành còn lại: backup expiry chỉ là runbook thủ công và local-only; các script không bao gồm offsite/production storage, không có khả năng thu hồi bản file đã tải, và file lỗi/chưa có metadata cần chính sách/đối soát riêng. M1 chưa nghiệm thu do còn thiếu bằng chứng dữ liệu/đánh giá.

## 18. L09 — baseline matching hai chiều trên localhost (16/10/2026)

L09 thêm scorer từ khóa xác định theo cấu hình `Matching:Baseline`. Token tiếng Việt được chuẩn hóa bỏ dấu, hỗ trợ một số token kỹ thuật như `.NET`, `C#`, `C++`, `Node.js`, `CI/CD`; stop words, top-k và trọng số trường có version/hash. Điểm là tổng trọng số token JD khớp chia tổng trọng số token JD, trả `retrieval_score` 0–1. Điểm 0–1 chỉ dùng để xếp retrieval; API bỏ mục zero-overlap, không dùng điểm 0–100 và không gọi LLM. JD chỉ có stop words được đếm riêng là `noQueryTerms`, không bị nhầm với một truy vấn hợp lệ nhưng không có CV phù hợp.

`GET /api/matching/jobs` cho candidate dùng bất kỳ CV version riêng nào thuộc chính họ, chỉ xét JD published/còn hạn của công ty chưa archive và owner active. `GET /api/matching/public-resumes` chỉ cho recruiter owner của tin đang nhận hồ sơ; phạm vi là lựa chọn CV public hiện tại có file nguồn ready và hash khớp, nên CV chỉ nộp riêng không lọt vào. Kết quả candidate khác không đọc tên/headline chưa được bật chia sẻ. Hai endpoint tính lại ở mỗi request, không cache hoặc lưu score trong DB để tránh kết quả cũ sau đổi quyền/ẩn CV/đóng tin. UI Candidate tìm việc bằng CV riêng; UI Recruiter tìm trên CV public. Manifest evaluator JSON local ghi IDs giả danh, điểm, metrics và hash input/config/assembly/revision, không chép nội dung CV/JD.

| Kiểm tra | Điều kiện | Kết quả 16/10 |
|---|---|---|
| Scorer/evaluator | Test .NET thuần trong project PostgreSQL và CLI local | 5 test scorer/evaluation kiểm tra chuẩn hóa Việt/technical tokens, trọng số, không bịa synonym, nhãn unknown/explicit 0, độ phủ và malformed JSON. Dataset smoke synthetic 3×3 chỉ xác nhận đường chạy; nó có nhãn thưa nên precision/nDCG là null. |
| API và quyền | PostgreSQL test DB ngẫu nhiên `careergap_test_*`; fake/storage local | 2 test API kiểm tra Candidate query private CV nhưng chỉ JD đang mở (draft/closed/expired bị bỏ); recruiter chỉ tìm CV public hiện hành, private submitted bị loại, profile display fields được tôn trọng, hide public lập tức biến mất, owner/role/direct ID bị chặn. Tổng 7 test matching nằm trong backend suite. |
| Full check | Root `./scripts/test.ps1` | **47 backend passed**, **3 frontend passed**, ESLint, TypeScript, Vitest và Vite build đạt. Cảnh báo chunk PDF khoảng 1,2 MB giữ nguyên. |
| Browser UI | API `127.0.0.1:5080`, Vite `127.0.0.1:5173`, app DB local, DOCX synthetic | `applications.spec.ts`: **1 passed** sau khi sửa assertion test dùng locator chứa nhiều kết quả. Candidate tìm JD khi CV chưa public; sau khi candidate public CV, recruiter owner tìm đúng CV; email không hiển thị. API/Vite được dừng sau test. Test thêm tài khoản recruiter/candidate, công ty/JD, public CV/file và đơn đã rút vào app DB/local storage; dữ liệu tổng hợp được giữ lại, không reset app DB. |
| Evaluator CLI | Root `./scripts/evaluate-matching-baseline.ps1`, không DB/API/provider | Chạy offline thành công; run `82a49331-bac3-4533-8520-8815da09d8ca`, manifest `.local/evaluation/matching-20261015T204842Z.json`; ghi hash dataset/config/assembly và source revision. Metric trong smoke không phải đánh giá chất lượng. |
| Docker | Root `docker build -t careergap:l09-local-20261016 .` | Build thành công; đây là image build, không phải runtime smoke/deploy/CI. |

L09 không thêm migration. Lượt E2E này chạy lại `applications.spec.ts` sau refactor L08 và kiểm tra hai widget matching trên cùng luồng ứng tuyển; các workflow E2E cũ còn lại chưa được chạy lại. Không có dataset CV/JD thật, nhãn giảng viên, rubric/threshold cuối, tải kho lớn hay bằng chứng về chất lượng ranking. JD→CV hiện đọc toàn bộ CV public phù hợp cho lần request local; hiệu năng kho lớn chưa đo. Working tree sau commit `5f4defe` chưa có CI mới và chưa push; M1/M2 chưa được tuyên bố nghiệm thu. Bước tiếp theo theo roadmap là GĐ3: extraction revisions CV/JD và xác nhận người dùng, rồi mẫu CV/PDF; gọi provider thật vẫn chờ checklist và kiểm chứng chi phí.

## 19. G3-L10 — revision kỹ năng CV có nguồn và xác nhận (16/10/2026)

L10 mở GĐ3 bằng một lát hẹp: job trích xuất thành công lưu `ResumeDataRevision` cho đúng CV version, cùng `AiJob.OutputJson` trong transaction hoàn tất. `structured_data` schema v1 chỉ chứa các kỹ năng được nêu trực tiếp; `source_text` giữ các segment parser đọc được và `evidence` giữ segment, quote nguyên văn cùng offset UTF-16 đã tính ở backend. Revision ghi hash nội dung, parser/schema, method/job, owner, số thứ tự và parent. Partial unique chặn tạo hai AI revision cho cùng job; hàng CV version được khóa khi cấp số. Trigger từ chối UPDATE/DELETE, ngoại trừ purge tài khoản với cờ transaction rõ ràng.

Candidate chỉ được đọc revision của CV version thuộc mình. Khi xác nhận, API kiểm tra revision mới nhất và `baseRevisionNo`, chỉ cho chọn kỹ năng nằm trong output AI, rồi ghi revision con `user_confirmed` cùng `confirmed_by/confirmed_at`. Kỹ năng Candidate tự bổ sung mang `source_type=user_statement`, không có quote/segment/offset và giao diện không hiển thị như trích dẫn từ file. Confirmed revision giữ parent để truy nguồn method/job; hiện chưa có API sửa revision đã xác nhận, pointer từ `candidate_public_resumes`, revision trong application snapshot hay consumer matching. Account purge xóa revisions trước versions/metadata AI; test cũng kiểm tra FK/immutability và đường xóa được ủy quyền.

| Kiểm tra | Điều kiện | Kết quả 16/10 |
|---|---|---|
| PostgreSQL/API | `./scripts/test.ps1`, migration trên DB `careergap_test_*` riêng | **48 backend passed**; test worker ghi revision và provenance, API Candidate owner-only/confirm/stale conflict, user statement tách quote, trigger immutable và purge revision. Migration mới `ResumeDataRevisions`; migration suite kiểm tra nâng schema có dữ liệu mẫu. |
| Frontend | Root `./scripts/test.ps1` | **3 frontend passed**; ESLint, TypeScript, Vitest và Vite production build đạt. Cảnh báo chunk PDF 1,2 MB vẫn còn. |
| Browser E2E | API `127.0.0.1:5080`, Vite `127.0.0.1:5173`, PostgreSQL/app DB local, DOCX synthetic | `resumes.spec.ts` và `applications.spec.ts`: **2 passed**. Candidate kiểm tra kỹ năng, xác nhận TypeScript và tự khai báo Go; regression L09 matching/ứng tuyển cũng đạt. Các bản ghi, file và test_credit tổng hợp còn trong local DB/storage; không reset database. |
| Migration/model | Root, PostgreSQL localhost/database `careergap` | `dotnet ef database update --project backend/CareerGap.Api` áp dụng migration thứ 14; `dotnet ef migrations has-pending-model-changes --project backend/CareerGap.Api` báo không có model diff. Up SQL chỉ tạo table/index/FK/trigger; Down gỡ table/trigger. |
| Docker | Root `docker build -t careergap:g3-l10-local-20261016 .` | Image build thành công; đây không phải runtime smoke, CI hoặc deploy. |

L10 dùng Fake provider với `test_credit`, không gọi DeepSeek hoặc dịch vụ ngoài. Đây chỉ là extraction kỹ năng định danh theo danh sách thử nghiệm; không đo recall/chính xác trên CV thật và không trích xuất toàn bộ CV/JD. Chưa có chuẩn skill dictionary/projection, tiêu thụ revision trong matching, chỉnh revision đã xác nhận, mẫu CV/PDF, nhãn giảng viên hoặc CI cho working tree. Vì vậy GĐ3/M3 chưa nghiệm thu. Tại thời điểm chốt L10, bước kế tiếp tuần tự là L11; kết quả triển khai sau đó được ghi ở mục 20.

## 20. G3-L11 — yêu cầu JD có revision và xác nhận của recruiter owner (16/10/2026)

L11 bổ sung `job_data_revisions` theo từng `job_version`. API chỉ cho recruiter owner đọc/tách/xác nhận; một khóa ngắn trên job/version tuần tự hóa số revision. Bộ tách local coi mỗi dòng không rỗng trong `requirements_text` là một yêu cầu, giữ nguyên source text, quote và offset UTF-16. Từ khóa Việt–Anh chỉ gợi ý `required`, `optional` hoặc `unspecified`; dấu hiệu thiếu/xung đột để `unspecified` để recruiter quyết định. Lát này không gọi Fake LLM hay provider thật và không tiêu `test_credit`.

Recruiter có thể giữ/bỏ từng dòng, đổi phân loại, thêm yêu cầu riêng hoặc xác nhận. Confirmation tạo child revision `user_confirmed` bất biến cùng người và thời điểm; yêu cầu tự khai báo mang `source_type=user_statement`, không có quote/offset từ JD. API khóa đúng revision mới nhất, kiểm tra `baseRevisionNo`, và từ chối đọc/ghi từ candidate, khách hoặc recruiter khác. Gọi extraction lặp lại trên một JD version trả revision sẵn có, tránh nhân bản cùng nội dung. Không thêm con trỏ revision hiện hành, skill taxonomy/projection hay revision ID vào application; matching L09 vẫn chạy như trước.

| Kiểm tra | Điều kiện | Kết quả 16/10 |
|---|---|---|
| PostgreSQL/API | Root `./scripts/test.ps1`; fixture tạo database `careergap_test_*` riêng | **49 backend passed**. `JobRevisionTests` kiểm tra owner scope, candidate bị từ chối, phân loại gợi ý/offset tiếng Việt, extraction idempotent, confirmation bỏ/chọn/đổi loại, user statement không bị nhận là quote, stale conflict và trigger immutable. Toàn suite có migration upgrade/downgrade test. |
| Frontend | Root `./scripts/test.ps1` | **3 frontend passed**; ESLint, TypeScript, Vitest và Vite production build đạt. Cảnh báo chunk PDF khoảng 1,2 MB còn. |
| Migration/model | Root, PostgreSQL local `localhost:55432/careergap` | Migration `20261015211227_JobDataRevisions` áp dụng; tổng 15 migrations. Up chỉ tạo bảng/FK/index/check/trigger, không backfill/xóa dữ liệu. Model snapshot không có diff chờ migration. |
| Browser E2E | API `localhost:5080`, Vite `127.0.0.1:5173`, DOCX giả trong `.local` | `jobs.spec.ts`, `resumes.spec.ts`, `applications.spec.ts`: **3 passed**. Bao gồm L11 recruiter review, L10 candidate provenance, L06 sàng lọc/note/block/đơn. Dữ liệu E2E và file giả còn trong DB/storage local; không reset DB ứng dụng. |
| Evaluator/Docker | Root `./scripts/evaluate-matching-baseline.ps1` và `docker build -t careergap:g3-l11-local-20261016 .` | Evaluator synthetic L09 chạy offline, run `83ec55f8-ff71-40bd-ba54-1bcc97f058cc`, manifest `.local/evaluation/matching-20261015T212833Z.json`; Docker build đạt. Đây không phải quality evaluation, CI hoặc runtime/deploy proof. |

Giới hạn L11: dòng trên JD không nhất thiết tương đương một yêu cầu nghiệp vụ; câu ghép lựa chọn như “A hoặc B” vẫn giữ nguyên trong một dòng nhưng schema hiện chưa biểu diễn quan hệ toán tử. Bộ gợi ý từ khóa chưa được đo precision/recall, nên xác nhận của recruiter là bắt buộc. Lúc chốt L11, `jobs.current_data_revision_id`, `job_revision_skills`, application revision pointer, chuẩn hóa skill và đánh giá trên nhãn thật còn mở. L06 ma trận trạng thái recruiter owner đã được người dùng xác nhận ngày 16/10; API có đúng các transition, và workflow ứng tuyển/sàng lọc E2E tiếp tục đạt. Working tree chưa có CI mới/publish; không tuyên bố nghiệm thu GĐ3/M3. Bước kế tiếp lúc đó là L12, kết quả được ghi ở mục 21.

## 21. G3-L12 — revision diagnostics bên cạnh baseline matching (16/10/2026)

L12 nối `ResumeDataRevision` và `JobDataRevision` vào hai endpoint matching L09 theo đúng CV/JD version. API chỉ chọn revision `user_confirmed` có confirmer phù hợp owner; backend kiểm tra schema/provenance và quote/offset với source trước khi tính. Candidate dùng được skill `document_span` và `user_statement` trên CV của mình. Recruiter search chỉ dùng lựa chọn CV public hiện hành; chỉ skill `document_span` được đưa vào đối chiếu, kỹ năng candidate tự khai báo bị loại. Sau khi đọc file và tính, API kiểm tra lại candidate/version/hash của public selection trước khi trả kết quả.

Diagnostic báo số yêu cầu có token overlap, tổng yêu cầu required đã xác nhận và số required có overlap; không trả skill names hay text trích nguồn. Đây là phép dò từ vựng, không khẳng định ứng viên đáp ứng yêu cầu, không tạo score 0–100 và không đổi `retrieval_score`, thứ tự hoặc tập kết quả của L09. Khi thiếu revision đã xác nhận hoặc data/provenance không hợp lệ, response trả trạng thái diagnostic riêng còn L09 tiếp tục độc lập. Vì items không có baseline overlap vốn vẫn bị L09 loại, L12 chưa làm tăng recall/tìm ra ứng viên ngoài tập L09 trả về. Không thêm migration, projection/taxonomy table, pointer/cache hoặc revision ID vào application/public selection.

Evaluator nhận optional `confirmedRevision` projections trên JSON synthetic/private dataset. Nó kiểm tra origin/schema/owner-confirmation flag và giới hạn/enum dữ liệu, rồi thống kê số cặp có nhãn đủ điều kiện, số cặp có overlap và counts theo relevance; candidate-view tính cả `user_statement`, recruiter/public-view chỉ tính `document_span`. Đây là bảng đếm chẩn đoán, không xếp hạng, không thay L09 metrics. `confirmedByOwner` là metadata do người chuẩn bị evaluation dataset cung cấp, không phải chữ ký hay xác minh DB; chưa có exporter và chưa có nhãn/dữ liệu có quyền sử dụng. Projection của smoke chỉ phục vụ kiểm tra đường chạy.

| Kiểm tra | Điều kiện | Kết quả 16/10 |
|---|---|---|
| PostgreSQL/API + evaluator unit | Root `./scripts/test.ps1`, mỗi integration test dùng database `careergap_test_*` ngẫu nhiên | **51 backend passed**. API tests xác nhận revision counts với `user_statement` ở Candidate view nhưng bị loại ở public recruiter view; evaluator test xác nhận revision chưa owner-confirmed bị loại và baseline/coverage không đổi. |
| Frontend | Root `./scripts/test.ps1` | **3 frontend passed**; ESLint, TypeScript, Vitest và Vite build đạt. Cảnh báo chunk PDF khoảng 1,2 MB còn. |
| Browser E2E | API Testing `127.0.0.1:5080`, Vite `127.0.0.1:5173`, DOCX synthetic | `applications.spec.ts`: **1 passed**. Candidate thấy thông báo fallback khi CV/JD chưa xác nhận; luồng tìm việc, public CV, recruiter search, apply, screening/note/block/rút đơn tiếp tục đạt. Fixture giả và bản ghi workflow được giữ trong local DB/storage; không reset database. API/Vite đã dừng sau test. |
| Migration/model | Root, `. ./scripts/dev-env.ps1` | 15 migrations vẫn hiện hành; `dotnet ef migrations has-pending-model-changes --project backend/CareerGap.Api` báo không có model changes. L12 không thêm schema/migration. |
| Evaluator CLI | Root `./scripts/evaluate-matching-baseline.ps1`, không DB/API/provider | Run `e93e7395-219f-48c3-a853-e24e0fd2201d`, manifest `.local/evaluation/matching-20261015T214353Z.json`; synthetic 5 labeled pairs, cả 5 có projection đủ điều kiện ở hai view, 3 cặp mỗi view có token overlap. Baseline metric vẫn ở L09 method; smoke không chứng minh chất lượng. Manifest `SourceRevision` là HEAD `5f4defe`, còn `SourceAssemblySha256` nhận diện assembly vừa build trong dirty working tree; không phải CI artifact. |
| Docker | Root `docker build --quiet -t careergap:g3-l12-local-20261016 .` | Image build thành công; digest `sha256:79343f372f48ea6eb3e1f71c5ba187b3e0a93198bea5c41f5559b07aefd89f95`. Không chạy container smoke/deploy. |

L12 chưa chuẩn hóa taxonomy kỹ năng; matcher dùng token extraction/stop words L09 và báo counts để kiểm thử đường dữ liệu. Một term chung vẫn có thể tạo overlap; phân loại optional/unspecified chưa có rubric/ngưỡng hoặc trọng số được duyệt. Evaluator fixture đánh dấu confirmed bằng metadata synthetic, không có source evidence trong manifest projection. Working tree chưa được CI xác nhận/publish; không gọi AI/search/email thật và không phát sinh `test_credit` từ chẩn đoán. GĐ3/M3, GĐ4/M4 và chất lượng matching vẫn chưa nghiệm thu.

**Bước tuần tự tiếp theo là L13 trong GĐ3:** mở rộng CV structured data ngoài skills schema v1 và xây flow tạo/sửa/xem trước/xuất PDF bằng template reviewable, giữ số lượng/layout mẫu cuối chưa xác nhận. Sau khi có dữ liệu hợp lệ mới đánh giá extraction trước chỉnh sửa; chỉ phát triển revision-based ranking sau khi có căn cứ nhãn/rubric. Không mở lại deploy hoặc gọi provider thật.

## 22. G3-L13 — CV builder nhập liệu và PDF `simple-v1` (16/10/2026)

L13 bổ sung một luồng Candidate tự nhập thông tin liên hệ, headline/summary, kỹ năng, kinh nghiệm, dự án, học vấn và chứng chỉ. Browser dựng preview và PDF A4 bằng `@react-pdf/renderer` cùng font Noto Sans. Candidate upload PDF qua pipeline file riêng tư hiện có, sau đó lưu nội dung cấu trúc vào `resume_data_revisions` dưới dạng `user_entered`, schema v2. Evidence chỉ ghi field paths và provenance `user_statement`, không giả làm quote từ tài liệu. Cùng một nội dung mới nhất retry idempotent; revision API khóa đúng CV version và kiểm tra owner/base revision. Mở lại revision rồi sửa sẽ upload file và tạo version/revision mới, không sửa lịch sử; nếu upload xong nhưng POST revision lỗi, UI giữ version để thử lại. Không thêm migration.

Phần tạo PDF và lưu revision là hai bước riêng: PDF đã upload không được coi là revision đã lưu nếu bước API thứ hai thất bại. Template `simple-v1` chỉ là một probe reviewable; số mẫu/layout cuối chưa được chốt. L12 diagnostics tiếp tục tiêu thụ confirmed skills schema v1 và bỏ qua schema builder v2. L13 chưa mở AI extraction cho các nhóm field mới, chưa có gallery template, chưa pin revision vào public selection/application, chưa kiểm tra trên CV thật hay đánh giá chất lượng extraction.

| Kiểm tra | Điều kiện | Kết quả 16/10 |
|---|---|---|
| Backend/API | Root `./scripts/test.ps1`, PostgreSQL integration DB `careergap_test_*` riêng | **52 backend passed**. `ResumeBuilderRevisionTests` kiểm tra candidate owner, schema/provenance, hash, latest/base revision, idempotent retry, tạo child revision, stale conflict, invalid input, quyền user khác và immutability trigger. Tổng số migration vẫn là 15; không có model/schema diff mới. |
| Frontend | Root `./scripts/test.ps1`; chạy lại `npm run lint` từ `frontend/` sau chỉnh sửa stress case | **3 frontend tests passed**; lint, typecheck, Vitest và Vite production build đạt. Còn cảnh báo bundle PDF renderer lớn khoảng 1,2 MB trước gzip; UI builder được lazy-load. |
| Browser E2E | API Testing `127.0.0.1:5080`, Vite `127.0.0.1:5173`, Chromium, dữ liệu tổng hợp; `frontend/e2e/cv-builder.spec.ts` | **1 passed**. Luồng nhập đủ nhóm field, xem preview, tải và lưu PDF/version/revision, tải lại revision, sửa và tạo version mới đạt. Không cần fixture DOCX; app DB/storage giữ dữ liệu tổng hợp, không reset database. Khi rà soát cuối, hai tiến trình localhost vẫn lắng nghe; `/api/health` trả `status=ok` và chúng được giữ nguyên. |
| PDF | PDF synthetic được tạo trong E2E, kiểm tra bằng `pdfinfo`, `pypdf` và Poppler | **2 trang A4**, PDF có text chọn/trích xuất được, tên tiếng Việt và số trang hiện diện; cả hai trang được render và xem, không thấy cắt chữ hoặc lỗi dấu. Đây không phải kiểm tra trên CV thật; trang 2 thưa do stress test summary dài. |
| Docker | Root `docker build --quiet -t careergap:g3-l13-local-20261016 .` | Build thành công, image digest `sha256:ba1e532fe2e13380c308e66b1811bda3cdd3bacf9e23702e6c2a11cb74f8930c`. Không chạy container smoke, CI hoặc deploy. |

Tất cả dữ liệu E2E là synthetic, provider là Fake/test_credit; không gọi DeepSeek/search/email thật và không có chi phí USD từ L13. Working tree chưa có CI run hoặc publish mới. Chưa có corpus CV được phép dùng, rubric/nhãn đã duyệt, phép đo extraction hoặc nghiệm thu M1/M2/M3. L08 backup/restore/expiry vẫn là quy trình local thủ công.

**Bước tuần tự tiếp theo là L14 trong GĐ3:** mở rộng extraction/rà soát có provenance cho các nhóm CV ngoài skills v1, dùng structured builder làm đích đối chiếu nhưng không coi nội dung tự khai báo là bằng chứng trong file. Trước khi đo chất lượng, chuẩn bị tập CV/JD tổng hợp hoặc dữ liệu có quyền sử dụng, protocol và nhãn tách biệt; không chặn phần parser/UI local độc lập và không gọi provider thật khi chưa kiểm chứng hạn mức.

## 23. Kiểm tra hồi quy L01–L09 trên localhost hiện tại (16/10/2026)

Kiểm tra lại các lát L01–L09 trên working tree có L01–L13 local. HEAD vẫn là `5f4defe`; các thay đổi chưa commit/push. API `127.0.0.1:5080` và Vite `127.0.0.1:5173` đã có sẵn trước lượt kiểm tra; `/api/health` trả `status=ok`. E2E dùng đúng các tiến trình đó và dữ liệu giả trong app DB/storage hiện hữu, không reset database.

| Kiểm tra | Lệnh/điều kiện | Kết quả 16/10 |
|---|---|---|
| Backend PostgreSQL | Root; `. ./scripts/dev-env.ps1`, đặt `CAREERGAP_TEST_DATABASE` từ connection string local; `dotnet test tests/CareerGap.Api.Tests/CareerGap.Api.Tests.csproj --no-restore -p:UseAppHost=false -p:BaseOutputPath=.local/l01-l09-validation/bin/` | **52 passed, 0 failed, 0 skipped** trên DB test riêng. Output cô lập tránh ghi đè DLL mà API đang nạp; app database không bị reset. |
| Frontend | Từ `frontend/`: `npm run lint`, `npm run typecheck`, `npm test -- --run`, `npm run build` | ESLint/typecheck/build đạt; **3 Vitest passed**. Vite vẫn cảnh báo chunk `react-pdf` khoảng 1,2 MB sau minify. |
| E2E L02–L08 và L09 UI | Từ `frontend/`, API/Vite localhost đang chạy, fixture `.local/fixtures/synthetic-vi.docx`, Chromium, một worker; chạy `accounts.spec.ts`, `jobs.spec.ts`, `resumes.spec.ts`, `applications.spec.ts`, `account-deletion.spec.ts` | **5 passed**. Bao phủ đăng ký/profile/reset fake, company/JD version và công khai, CV private/version/public-hide, apply/snapshot/notification/status/note/block/withdraw, chặn apply khi candidate còn bị block, matching hai chiều và request/cancel deletion. Assertion L09 không bắt buộc job mới phải lọt top-20 khi app DB giữ các job synthetic từ lần chạy trước. |
| Evaluator L09 | Chạy assembly vừa build trong `.local/l01-l09-validation/bin/` trực tiếp; JSON smoke synthetic, không DB/API/provider | Run `8d6ac5d8-5fa6-44c6-8897-d94a428cc9fe`, dataset `synthetic-matching-smoke-v1`, 3 query mỗi chiều; synthetic recall@20 trung bình 0,833 ở cả hai chiều. Đây chỉ là smoke, không phải phép đo chất lượng hoặc nhãn được thẩm định. Manifest `.local/evaluation/matching-l01-l09-20261016.json`. |
| Migration/model | Root; `dotnet ef migrations has-pending-model-changes --project backend/CareerGap.Api --no-build`; `dotnet ef migrations list --project backend/CareerGap.Api --no-build` | Không có model diff; danh sách mã nguồn có 15 migration. Lệnh build mặc định riêng bị API đang chạy khóa DLL; test backend dùng output cô lập và EF `--no-build` đạt. Không áp dụng migration mới hoặc sửa app DB. |
| Docker | Root `docker build --quiet -t careergap:l01-l09-verify-20261016 .` | Build đạt; digest `sha256:e31dbdc2d24d512127aa3f68808b5dab00f5c9158366b4b16eb7722b97b8e265`. Không khởi chạy container smoke ở lượt này. |
| CI L01 | GitHub Actions run [`38774073395`](https://github.com/LeHoangLich/CareerGapAI/actions/runs/38774073395) | Trang run xác nhận `Success` cho commit `5f4defe` và jobs frontend/backend/Docker. CI chỉ chứng nhận commit đó, không chứng nhận working tree local hiện tại. |

Trong E2E đầu tiên, locator profile cũ bị mơ hồ sau khi CV builder thêm field cùng nhãn; đã giới hạn locator vào panel hồ sơ và spec chạy lại đạt. Assertion matching cũng được chỉnh để tôn trọng top-20 có giới hạn khi local DB giữ fixture giữa các lần chạy; ranking/API không đổi. Local service thật, corpus có quyền sử dụng, rubric/nhãn, provider thật, CI cho working tree và nghiệm thu M1/M2 vẫn còn ngoài bằng chứng trên. L01–L09 có các lát local và hồi quy đã chạy; không lấy kết quả synthetic thay cho đánh giá chất lượng.
