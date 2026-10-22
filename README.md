# CareerGap AI

> Giai đoạn C ngày 22/10: đã có matching hai chiều, điểm 0–100 và bằng chứng, phân tích hồ sơ theo JD/CV lúc nộp, user quota và cache có kiểm quyền. [Báo cáo C](project-docs/report/stage-c-2026-10-22.md), [API/rubric/cách chạy evaluator](project-docs/phase-04-matching.md). Migration 16 chỉ được áp dụng trong môi trường kiểm thử riêng; chạy migration bằng bước riêng trước khi dùng tính năng ở DB local khác. Các ghi nhận “chưa thực hiện C” dưới đây là lịch sử. M3 chất lượng đại diện vẫn chờ; tiếp tục chỉ localhost.

> Kiểm chứng M3 local mới ngày 21/10: [94 backend, 9 E2E, DOCX gốc và JD parser v3](project-docs/report/m3-local-acceptance-2026-10-21.md). Chức năng local đạt các ca đã chạy; chất lượng đại diện còn native vi CV, người rà nhãn và quyền provider cho corpus mới. Không phí mới, không deploy/C; các ghi nhận “hoãn/chưa có corpus” bên dưới thuộc các lượt trước.

> Chốt chức năng B ngày 21/10: [probe DeepSeek thật và browser workflow PDF/apply/snapshot](project-docs/report/stage-b-provider-2026-10-21.md) đã đạt trên synthetic; trần DeepSeek 2 USD cho toàn giai đoạn phát triển, không reset theo lượt. M3 chất lượng trên corpus đại diện vẫn hoãn, chưa thực hiện C.

> Điểm đọc hiện trạng duy nhất: [baseline, CI và phạm vi L01–L13](project-docs/current-status.md). [Giai đoạn A](project-docs/report/stage-a-2026-10-16.md) có checklist nghiệm thu, kết quả dựng lại và đầu vào đánh giá còn chờ.

Bản thử localhost có JD công khai để khách/ứng viên tìm kiếm; recruiter quản lý công ty/JD theo version. Candidate quản lý profile, CV private nhiều version, nộp một version cụ thể, theo dõi trạng thái/lịch sử và rút đơn. Recruiter sàng lọc theo ma trận đã chốt, ghi chú nội bộ riêng tư và block theo từng tin; snapshot giữ version CV/JD lúc nộp. L07 hỗ trợ ứng viên chủ động chọn một CV version công khai; recruiter nhận file gốc, ứng viên khác chỉ nhận bản DOCX dựng lại từ text và đã quét liên hệ nhận diện được. L08 hiện thực cục bộ yêu cầu xóa 7 ngày, purge, hết hạn đơn 90 ngày và dọn file upload pending sau 24 giờ. L09 có baseline matching từ khóa hai chiều trên localhost, trả `retrieval_score` 0–1; đây không phải điểm AI sâu và chưa được đánh giá chất lượng thật. L10 lưu revision kỹ năng CV để Candidate xác nhận provenance; L11 lưu revision yêu cầu theo JD version để recruiter owner rà soát/xác nhận. L12 bổ sung chẩn đoán term-overlap riêng từ các revision đã xác nhận của đúng CV/JD version; giữ nguyên điểm/thứ tự L09, không tạo điểm phù hợp tổng hợp. L13 có trình tạo CV `simple-v1`: ứng viên nhập dữ liệu, xem trước/tải PDF tiếng Việt rồi lưu thành file version riêng tư cùng revision `user_entered` schema v2 để mở lại/sửa. Chủ dự án đã chọn một mẫu một cột simple-v1 cho GĐ3. B/L14 thêm full CV schema v3, sửa/xác nhận theo field, parser process con và JD v2; giữ nguyên dữ liệu v1/v2. Builder nạp dữ liệu đã xác nhận và phục hồi save dở sau reload; PDF tạo ra đã được kiểm tra dùng khi ứng tuyển. [Chi tiết B và giới hạn](project-docs/report/stage-b-2026-10-16.md). E2E dùng Fake/test_credit, không gọi provider thật. Backup/restore/expire hiện là runbook local thủ công; CI baseline và các thay đổi sau đó được phân biệt tại [hiện trạng chuẩn](project-docs/current-status.md); chưa có bằng chứng production hoặc nghiệm thu M1/M2/M3 tổng thể.

**Ưu tiên từ 15/10/2026: dừng deploy, hoàn thiện sản phẩm trên localhost trước.** Phát triển React/API/PostgreSQL local, kiểm thử nghiệp vụ và hoàn thiện ba luồng cốt lõi theo [roadmap](project-docs/03-roadmap.md). Hosting, DNS và kiểm chứng production được hoãn; chỉ mở lại deploy khi sản phẩm hoàn thiện trên localhost và người dùng yêu cầu. CI build/test và Docker smoke local vẫn phục vụ phát triển.

Nguồn nghiệp vụ ở [project-docs](project-docs/01-system-specification.md). Theo dõi [task GĐ1](project-docs/tasks/phase-01-foundation.md), [hợp đồng và giới hạn](project-docs/phase-01-implementation.md), [kế hoạch đánh giá](project-docs/evaluation/phase-01-evaluation.md).

Điền dịch vụ khi sẵn sàng tại [checklist S01–S12](project-docs/phase-01-services-checklist.md), chỉ ghi trạng thái/thông tin không bí mật. Quyền CV ẩn liên hệ và [chính sách lưu/xóa 90/7/14 ngày](project-docs/phase-01-data-retention.md) đã được người dùng chốt; L06 hiện thực block/nộp lại theo tin, L07 hiện thực publish/hide CV và quyền đọc theo role, L08 có API/worker localhost cho thu hồi và purge. Bằng chứng hiện tại chỉ bao phủ implementation local với fake và file storage local.

## Chạy local (PowerShell, từ thư mục repository)

Cần Git, .NET SDK 10.0.401, Node 24.16.0/npm và Docker engine Linux. PostgreSQL local/CI dùng 16.10. Chỉ localhost, không dùng cấu hình development này làm demo Internet.

1. Sao chép `.env.example` thành `.env`, thay mật khẩu local ở cả hai dòng PostgreSQL/connection string cho khớp. `.env` bị Git bỏ qua. Không dán secrets vào chat.
2. Chạy:

```powershell
. ./scripts/dev-env.ps1
docker compose up -d db
dotnet tool restore
dotnet restore tests/CareerGap.Api.Tests/CareerGap.Api.Tests.csproj --locked-mode
dotnet ef database update --project backend/CareerGap.Api
```

Migration là bước riêng; API không tự sửa schema lúc khởi động. Quyền tạo/drop database chỉ cần cho integration test trên PostgreSQL local; runtime production phải dùng tài khoản riêng không có DDL.

Trên giao diện local, đăng ký Candidate hoặc Recruiter; Candidate được tạo profile cùng transaction, còn Admin không thể tự đăng ký. Khôi phục mật khẩu hiện dùng fake mailbox trong Development/Testing và không gửi email thật. API mailbox chỉ khả dụng local/test; đây là kiểm thử logic, không chứng minh email giao nhận.

Khách có thể tìm JD công khai. Recruiter sở hữu công ty có thể tạo tin nháp, sửa thành version mới, công khai hoặc đóng; Candidate đăng nhập để lưu việc, đặt tên CV và thêm file thành version mới. Candidate chọn CV version cụ thể khi nộp vào tin còn nhận đơn. Application ghim version CV/JD lúc gửi, lưu history ban đầu và thông báo cùng transaction; cùng submission key/input trả về đơn cũ. Recruiter phụ trách tin xem/tải hồ sơ qua application; tải trực tiếp bằng file ID vẫn bị chặn. Đơn rejected/withdrawn chỉ còn hiện và cho recruiter đọc snapshot tối đa 90 ngày; candidate chính chủ vẫn xem snapshot của mình.

Candidate có thể công khai một version CV riêng và ẩn/thay bằng version khác; upload không tự public. Recruiter mở file gốc qua lựa chọn hiện hành. Candidate khác chỉ mở URL gắn đúng candidate/version và nhận DOCX mới dựng từ các đoạn text parser đọc được; ảnh, hyperlink, metadata file nguồn không được chép sang. Hệ thống bỏ và quét lại email/điện thoại/URL nhận diện được, lưu hash nguồn cùng phiên bản phương pháp, và không trả file gốc khi bản dẫn xuất chưa sẵn sàng. Các trường hợp contact bị viết theo cách parser/regex không nhận diện được vẫn cần kiểm thử dữ liệu thật trước khi tuyên bố độ phủ đầy đủ.

L08 giới hạn quyền đọc terminal application ở mốc 90 ngày và purge dữ liệu riêng của đơn; worker dọn đơn theo batch. Tài khoản xóa bị thu hồi quyền ngay, có 7 ngày để hủy, sau đó worker purge và ghi ledger xóa object có retry. File `pending` không tham chiếu được dọn sau khoảng đệm 24 giờ; lỗi xóa object được giữ trạng thái retry. Runbook dưới đây chỉ backup/restore PostgreSQL + local files + DP keys trong môi trường Development, không thay thế quy trình backup production.

Để chạy E2E nền tảng cũ, tạo tài khoản ứng viên giả bằng email thuộc `example.invalid` và mật khẩu mạnh riêng trong biến môi trường; không ghi literal mật khẩu vào script/history. Có thể nhập bằng prompt:

```powershell
$env:DEMO_EMAIL = Read-Host 'Email giả cho local'
$taskCredential = Get-Credential -UserName $env:DEMO_EMAIL -Message 'Mật khẩu local mới (ít nhất 12 ký tự, có hoa/thường/số/ký hiệu)'
$env:DEMO_PASSWORD = $taskCredential.GetNetworkCredential().Password
dotnet run --project backend/CareerGap.Api -- --create-demo-user
Remove-Item Env:DEMO_PASSWORD
dotnet run --no-build --project backend/CareerGap.Api --urls http://127.0.0.1:5080
```

Lệnh này chỉ chạy Development/Testing và là tiện ích tạo fixture; mỗi lần chạy tạo tài khoản mới, không đổi mật khẩu tài khoản có sẵn. Nếu cấp Admin cho kiểm thử, chỉ đặt role qua lệnh local có kiểm soát, không từ giao diện đăng ký.

Trong terminal khác:

```powershell
cd frontend
npm ci
npm run dev
```

Mở [giao diện local](http://127.0.0.1:5173). API ở cổng 5080, Vite proxy `/api`; [OpenAPI development](http://127.0.0.1:5080/openapi/v1.json). Cookie/CSRF dùng chung origin phía browser. File development lưu `.local/files`, không phải storage cloud bền vững.

## Kiểm tra

Từ root, sau khi Docker DB sẵn sàng:

```powershell
./scripts/test.ps1
docker build -t careergap:phase01 .
./scripts/evaluate-matching-baseline.ps1
```

Backend tests tự tạo/drop database có tên `careergap_test_*` ngẫu nhiên, không reset database ứng dụng. Chúng dùng PostgreSQL thật, fake provider và hạn mức login cao hơn chỉ trong environment Testing. Kết quả TRX ở `tests/CareerGap.Api.Tests/TestResults/` (Git bỏ qua).

Evaluator matching mặc định chạy bộ [synthetic smoke](project-docs/evaluation/matching-baseline-smoke.json), không gọi DB/API/provider và ghi manifest vào `.local/evaluation/`. Nếu dataset có optional `confirmedRevision` projection, evaluator chỉ thêm thống kê term-overlap cho cặp có CV/JD revision được xác nhận bởi owner; thống kê tách candidate-view/public-view, không tham gia xếp hạng hoặc sửa metric L09. Truyền `-Dataset` cho dataset riêng; dùng ID giả danh, không dùng email/tên hay commit CV/JD thật. Manifest ghi hash đầu vào, cấu hình và assembly API, lưu ID/điểm/metric nhưng không chép văn bản. Bộ smoke xác nhận đường chạy, không phải kết quả chất lượng hoặc nhãn đã được thẩm định. Xem [quy tắc dữ liệu và phép đo](project-docs/evaluation/phase-01-evaluation.md).

Runbook bảo trì localhost từ root: dừng API/worker, chạy `./scripts/backup-local.ps1 -OfflineConfirmed`; có thể xuất ledger mới bằng `./scripts/export-deletion-ledger.ps1`. Restore bằng `./scripts/restore-local-backup.ps1 -BackupDirectory '<đường dẫn trong .local/backups>' -DeletionLedgerPath '<ledger hiện hành>'` tạo database `careergap_restore_*` và không bật API. Chạy `./scripts/expire-local-backups.ps1 -DeletionLedgerPath '<ledger hiện hành>'` thủ công để xóa các gói backup đã đến hạn; script không được lên lịch tự động và áp thời hạn bảo thủ trên toàn bộ gói backup local, nên có thể xóa cả gói cũ hơn không chứa chính dữ liệu purge. Chỉ áp dụng các script vào dữ liệu `.local` do chúng quản lý.

Từ `frontend/`: `npm run lint`, `npm run typecheck`, `npm test`, `npm run build`, `npm audit --audit-level=high`. PDF renderer lazy-load; chunk PDF lớn là cảnh báo build đã biết, không tải vào màn hình login.

E2E cần API/Vite chạy và Playwright Chromium (`npx playwright install chromium`). Năm bài `accounts.spec.ts`, `jobs.spec.ts`, `resumes.spec.ts`, `applications.spec.ts`, `cv-builder.spec.ts` tự tạo tài khoản giả để kiểm tra đăng ký/profile, company/JD, CV private nhiều version, tạo/xem trước/tải/lưu/mở lại PDF tiếng Việt, và ứng tuyển/sàng lọc giữa hai vai trò, gồm status/history, ghi chú nội bộ, block/apply và quyền snapshot. `applications.spec.ts` còn kiểm tra candidate tìm JD bằng CV riêng trước khi public, recruiter xếp hạng CV sau khi public và không thấy email; L12 kiểm tra UI ghi rõ fallback khi revision CV/JD chưa được xác nhận. Bài CV upload/ứng tuyển cần DOCX giả; đặt `CAREERGAP_DOCX_FIXTURE` trỏ tới fixture trước khi chạy. Bài `cv-builder.spec.ts` không cần fixture file. `foundation.spec.ts` là luồng nền tảng cũ, cần `DEMO_EMAIL`, `DEMO_PASSWORD` và `CAREERGAP_DOCX_FIXTURE`; không chạy được nếu chưa cấu hình hai biến demo. Trong Development, giới hạn đăng ký là 5/phút; chạy workflow bằng API environment Testing để dùng fake/local và giới hạn test cao. Không bật trace có mật khẩu. Bản ghi tài khoản/file synthetic giữ trong DB/storage local; không reset database ứng dụng hoặc commit dữ liệu đăng nhập.

Để thử React static cùng .NET trên Linux, sau khi database local đã có migrations và tài khoản giả:

```powershell
# Từ root, dùng .env local đã cấu hình. Cổng DB trong network Docker là 5432.
docker compose -f compose.yaml -f compose.smoke.yaml up -d --build api
# Trong terminal frontend đã đặt tài khoản/fixture:
$env:CAREERGAP_BASE_URL = 'http://127.0.0.1:9080'
npm run e2e
# Từ root, dừng API thử mà không xóa database/volume:
docker compose -f compose.yaml -f compose.smoke.yaml stop api
```

Smoke chỉ bind localhost và dùng Testing/Fake/Local storage; file trong container không bền vững khi thay container. Không dùng compose smoke làm cấu hình production hoặc dùng chung metadata file với máy khác để suy ra file đã tồn tại ở cả hai nơi.

Tạo bộ fixture giả bằng `python scripts/generate-fixtures.py` nếu đã có `reportlab`; Python chỉ là tooling dữ liệu, không phải backend. Hoặc upload DOCX giả do người thực hiện tạo. Kiểm tra parser local không in nội dung:

```powershell
. ./scripts/dev-env.ps1
dotnet run --project backend/CareerGap.Api -- --probe-file D:/CareerGapAI/.local/fixtures/synthetic-vi.docx
```

Chuẩn bị dữ liệu đánh giá bằng Python 3.10+ (chỉ thư viện chuẩn), chạy từ root:

```powershell
python scripts/prepare-synthetic-corpus.py
python scripts/validate-evaluation-corpus.py .local/evaluation/synthetic-corpus.json --verify-files --output .local/evaluation/corpus-check.json
python -m unittest discover -s tests/evaluation -v
./tests/evaluation/test-provider-readiness.ps1
./scripts/check-deepseek-readiness.ps1
```

Preflight plan public mặc định trả **exit 1 có chủ đích** vì probe 21/10 đã đóng, không có dispatch mới. Key đã xác thực và probe riêng đã đạt; quyền phát triển trong trần DeepSeek 2 USD còn hiệu lực theo ledger, không tự mở lại cap đã dùng. Manifest hiện chỉ synthetic; xem [giao thức, template và giới hạn](project-docs/evaluation/stage-a-corpus-protocol.md), [runbook provider](project-docs/evaluation/deepseek-probe-runbook.md). Không dùng kết quả smoke làm bằng chứng chất lượng thực tế.

## GĐ3/B — đọc đầy đủ, xác nhận và tạo CV

Nút “Đọc đầy đủ” dùng contract CV v3 riêng, giữ v1 kỹ năng/v2 builder đã lưu. UI cho sửa/bỏ/bổ sung với quote hoặc nhãn tự xác nhận, theo đúng version/owner; reload theo dõi job đã nhận. JD v2 bổ sung yêu cầu tách, số năm và nhóm lựa chọn để recruiter rà. [Hợp đồng/giới hạn](project-docs/phase-03-extraction-contract.md), [báo cáo B](project-docs/report/stage-b-2026-10-16.md).

Để chạy bộ smoke extraction từ root (Python có reportlab; không cần DB/key):

```powershell
python scripts/generate-cv-extraction-fixtures.py
dotnet build backend/CareerGap.Api
./scripts/evaluate-cv-extraction.ps1
```

Khi chạy E2E, đặt `CAREERGAP_FULL_CV_FIXTURE` vào `.local/evaluation/cv-extraction-fixtures/full-cv-vi.docx`. `stage-b.spec.ts` kiểm tra full review/reload/convert và phục hồi lỗi manual revision sau upload, PDF → apply → recruiter tải đúng snapshot sau khi thêm version. Cả suite có 9 spec; foundation vẫn cần demo credentials giả. Synthetic/Fake chỉ kiểm tra code, không là quality corpus.

Builder giữ draft dang dở trong **sessionStorage theo user** sau upload, có nút hoàn tất/bỏ và xóa khi thành công/đăng xuất; đóng/xóa phiên browser có thể mất draft. Không tự xóa file đã upload khi bỏ draft. Mẫu duy nhất simple-v1 đã được chủ dự án chốt cho GĐ3. Sau E2E có thể kiểm tra PDF bằng Python có pdfplumber:

```powershell
python scripts/check-generated-cv-pdfs.py .local/stage-b-20261016/evidence/builder-long.pdf .local/stage-b-20261016/evidence/builder-short.pdf
```

Bổ sung local 19/10: [chấm captures trước sửa](project-docs/evaluation/cv-capture-evaluation.md), [runbook full CV v3](project-docs/evaluation/deepseek-probe-runbook.md), [checklist M3](project-docs/phase-03-acceptance.md), [report](project-docs/report/stage-b-followup-2026-10-19.md). Full CV real yêu cầu `Ai__FullCvMaxOutputTokens` riêng. Mặc định ứng dụng/CI vẫn Fake; probe thật 21/10 đã được cho phép và hoàn tất trong cap 0,05 USD, xem report mới ở đầu README. E2E có thể đặt `CAREERGAP_E2E_EVIDENCE_DIRECTORY` vào thư mục evidence mới để giữ PDF lịch sử.

## Triển khai đã dừng — tham khảo cho giai đoạn sau

Không thực hiện các bước production dưới đây trong giai đoạn hiện tại. Không cần cung cấp hosting/domain để tiếp tục xây localhost; thông tin dịch vụ đã có được giữ nguyên, không tự xóa hoặc thay đổi cloud. Thử provider thật từ localhost là công việc riêng cần cấu hình và ngân sách được cho phép; fake không chứng minh chất lượng AI hay gửi email thật.

Dockerfile đóng gói React static cùng .NET, port mặc định 8080. Đặt `ASPNETCORE_HTTP_PORTS` theo cổng nền tảng; không chạy Vite trên cloud. Cấu hình production bắt buộc private S3 storage, connection string, chứng thư Data Protection và allowed hosts thực tế. Tham khảo [runbook](project-docs/phase-01-implementation.md).

Repository cá nhân do chủ dự án publish tại [GitHub](https://github.com/LeHoangLich/CareerGapAI). Bản chuẩn và CI đúng SHA xem [hiện trạng](project-docs/current-status.md); [báo cáo giai đoạn A](project-docs/report/stage-a-2026-10-16.md) ghi kiểm tra mới và điều kiện M1/M2 còn thiếu. Không tự push hoặc mở lại deploy. Chưa có public demo hoặc bằng chứng private bucket/search/email thật; DeepSeek full CV đã qua probe thật trên synthetic ngày 21/10.
