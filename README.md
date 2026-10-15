# CareerGap AI

Bản thử GĐ1: đăng nhập bằng tài khoản cấp local → upload PDF/DOCX private → đọc theo quyền → tạo job trích xuất kỹ năng. **Mặc định là mô phỏng từ khóa**, không gọi DeepSeek và không chấm mức phù hợp CV–JD. M1 localhost chưa được nghiệm thu đầy đủ.

**Ưu tiên từ 15/10/2026: dừng deploy, hoàn thiện sản phẩm trên localhost trước.** Phát triển React/API/PostgreSQL local, kiểm thử nghiệp vụ và hoàn thiện ba luồng cốt lõi theo [roadmap](project-docs/03-roadmap.md). Hosting, DNS và kiểm chứng production được hoãn; chỉ mở lại deploy khi sản phẩm hoàn thiện trên localhost và người dùng yêu cầu. CI build/test và Docker smoke local vẫn phục vụ phát triển.

Nguồn nghiệp vụ ở [project-docs](project-docs/01-system-specification.md). Theo dõi [task GĐ1](docs/tasks/phase-01-foundation.md), [hợp đồng và giới hạn](docs/phase-01-implementation.md), [kế hoạch đánh giá](docs/evaluation/phase-01-evaluation.md).

Điền dịch vụ khi sẵn sàng tại [checklist S01–S12](docs/phase-01-services-checklist.md), chỉ ghi trạng thái/thông tin không bí mật. Quyền CV ẩn liên hệ, nộp lại/block theo tin và [chính sách lưu/xóa 90/7/14 ngày](docs/phase-01-data-retention.md) đã được người dùng chốt; chưa phải các tính năng GĐ2 đã hiện thực trong prototype.

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

Tạo một ứng viên giả bằng email thuộc `example.invalid` và mật khẩu mạnh riêng trong biến môi trường; không ghi literal mật khẩu vào script/history. Có thể nhập bằng prompt:

```powershell
$env:DEMO_EMAIL = Read-Host 'Email giả cho local'
$taskCredential = Get-Credential -UserName $env:DEMO_EMAIL -Message 'Mật khẩu local mới (ít nhất 12 ký tự, có hoa/thường/số/ký hiệu)'
$env:DEMO_PASSWORD = $taskCredential.GetNetworkCredential().Password
dotnet run --project backend/CareerGap.Api -- --create-demo-user
Remove-Item Env:DEMO_PASSWORD
dotnet run --no-build --project backend/CareerGap.Api --urls http://127.0.0.1:5080
```

Lệnh tạo user chỉ chạy Development/Testing, không có public registration hoặc seed ngầm. Không gửi `DEMO_ROLE=Admin` từ frontend; role chỉ cấu hình cho lệnh local có kiểm soát. Mỗi lần chạy tạo tài khoản mới, không đổi mật khẩu tài khoản có sẵn.

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
```

Backend tests tự tạo/drop database có tên `careergap_test_*` ngẫu nhiên, không reset database ứng dụng. Chúng dùng PostgreSQL thật, fake provider và hạn mức login cao hơn chỉ trong environment Testing. Kết quả TRX ở `tests/CareerGap.Api.Tests/TestResults/` (Git bỏ qua).

Từ `frontend/`: `npm run lint`, `npm run typecheck`, `npm test`, `npm run build`, `npm audit --audit-level=high`. PDF renderer lazy-load; chunk PDF lớn là cảnh báo build đã biết, không tải vào màn hình login.

E2E cần API/Vite chạy, một tài khoản giả đã cấp, Playwright Chromium (`npx playwright install chromium`) và DOCX giả. Đặt `DEMO_EMAIL`, `DEMO_PASSWORD`, `CAREERGAP_DOCX_FIXTURE` trong terminal, rồi chạy `npm run e2e` ở `frontend/`. Không bật trace có mật khẩu. Kết quả screenshot/PDF ở `.local/evidence/`, không commit dữ liệu đăng nhập.

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

## Triển khai đã dừng — tham khảo cho giai đoạn sau

Không thực hiện các bước production dưới đây trong giai đoạn hiện tại. Không cần cung cấp hosting/domain để tiếp tục xây localhost; thông tin dịch vụ đã có được giữ nguyên, không tự xóa hoặc thay đổi cloud. Thử provider thật từ localhost là công việc riêng cần cấu hình và ngân sách được cho phép; fake không chứng minh chất lượng AI hay gửi email thật.

Dockerfile đóng gói React static cùng .NET, port mặc định 8080. Đặt `ASPNETCORE_HTTP_PORTS` theo cổng nền tảng; không chạy Vite trên cloud. Cấu hình production bắt buộc private S3 storage, connection string, chứng thư Data Protection và allowed hosts thực tế. Tham khảo [runbook](docs/phase-01-implementation.md).

Repository cá nhân đã được người dùng publish tại [GitHub](https://github.com/LeHoangLich/CareerGapAI). [CI run 38718678231](https://github.com/LeHoangLich/CareerGapAI/actions/runs/38718678231) của commit `bdb3aeb` đã thành công cho frontend/backend/Docker; các thay đổi local sau đó chưa có CI run mới. Dự án do một người thực hiện; M1 cần người thực hiện dựng lại theo hướng dẫn, không cần xác nhận nhiều thành viên. Chưa có URL demo public, DeepSeek thật, private bucket thật, search hoặc email thật. Không coi Docker build hay fake E2E là bằng chứng các phần này hoạt động. Không có lời gọi trả phí tự động trong CI.
