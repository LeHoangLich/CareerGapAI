# Dependency GĐ1

Kiểm tra ngày 15/10/2026 từ `package.json` đã cài và NuGet `.nuspec` đúng phiên bản. Lockfile npm/NuGet và `dotnet-tools.json` giữ phiên bản thực tế; không dùng latest trong CI. Đây là kiểm kê nhu cầu/giấy phép kỹ thuật, không xác nhận dịch vụ cloud được cấp quyền.

| Dependency | Phiên bản | Giấy phép | Nhu cầu |
|---|---|---|---|
| React / React DOM | 19.3.1 | MIT | Giao diện SPA theo stack đã chốt |
| Vite | 8.3.4 | MIT | Dev proxy và production build |
| @vitejs/plugin-react | 6.1.2 | MIT | Tích hợp React/Vite cùng major tương thích |
| TypeScript | 5.9.3 | Apache-2.0 | Kiểm tra kiểu frontend |
| @types/react / @types/react-dom | 19.3.0 | MIT | Khai báo kiểu React |
| ESLint | 9.39.2 | MIT | Lint; dòng này được npm đánh dấu không còn hỗ trợ, cần lập đợt nâng có kiểm chứng riêng |
| typescript-eslint | 8.71.2 | MIT | Lint TS; đã chọn bản cùng major loại cảnh báo audit ban đầu |
| Vitest | 5.0.4 | MIT | Unit test thông báo lỗi/quota |
| @playwright/test | 1.64.1 | Apache-2.0 | Browser E2E với download/PDF và proxy |
| @react-pdf/renderer | 4.9.0 | MIT | Thử xuất PDF client, lazy-load |
| @fontsource/noto-sans | 5.3.0 | OFL-1.1 | Font UI local, không gọi CDN |
| Identity EF / DataProtection EF / OpenAPI / EF Design | 10.0.13 | MIT | Tài khoản, key ring, hợp đồng, sinh migrations |
| Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.3 | PostgreSQL | EF provider PostgreSQL 16 |
| PdfPig | 0.1.16 | Apache-2.0 | Đọc PDF và thứ tự khối văn bản |
| DocumentFormat.OpenXml | 3.5.1 | MIT | Đọc DOCX không cần Microsoft Word |
| AWSSDK.S3 | 4.0.104.4 | Apache-2.0 | Adapter storage private; chưa thử provider thật |
| Microsoft.AspNetCore.Mvc.Testing | 10.0.13 | MIT | HTTP integration test |
| Microsoft.NET.Test.Sdk | 17.12.0 | MIT | Test runner |
| xunit / xunit.runner.visualstudio | 2.9.2 / 2.8.2 | Apache-2.0 | Test/assertions |
| dotnet-ef local tool | 10.0.13 | MIT | Sinh/review/chạy migrations có kiểm soát |

Noto Sans full TTF cho PDF được lấy từ [nguồn chính thức](https://github.com/notofonts/noto-fonts/tree/main/hinted/ttf/NotoSans), giữ [OFL](../frontend/public/fonts/OFL.txt) cạnh font; không sửa tên/font. SHA-256 của NotoSans-Regular.ttf: `B85C38ECEA8A7CFB39C24E395A4007474FA5A4FC864F6EE33309EB4948D232D5`. Không dùng font hệ điều hành không rõ quyền phân phối.

Không thêm các thư viện router/query/form/UI vào prototype khi chưa có nhu cầu cụ thể. `fetch` và React state đủ cho luồng GĐ1; đây không phải quyết định loại chúng khỏi các giai đoạn sau. `reportlab`/`pypdf`/Poppler chỉ là tooling tạo/kiểm tra fixture/PDF ngoài backend, không dependency chạy ứng dụng.
