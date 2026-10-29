# Multi-view SPA — Bước 1, 28/10/2026

Đã thực hiện theo yêu cầu owner dùng skill `design-taste-frontend` và cho phép viết code Bước 1. Phạm vi: điều hướng và cách ly màn hình; chưa triển khai bố cục hai cột, onboarding thu gọn hoặc empty state của Bước 2.

## Thay đổi

- `frontend/src/navigation.ts`: ánh xạ hash vào view theo session/role, giữ các hash cũ, Back/Forward/reload; focus main và cuộn về đầu khi chuyển view. Query reset mật khẩu không có hash mở đúng màn hình access; skip-link không ghi đè hash đang mở.
- `frontend/src/persistentView.tsx`: portal vào container ổn định. Chỉ container của view hiện hành được gắn vào document. Các view khác giữ component, refs và native form controls trong container đã tháo khỏi document; không dùng CSS để giấu các section trong trang. Effects/polling hiện có tiếp tục hoạt động trong vòng đời phiên. Không bổ sung lưu bản nháp hay mật khẩu vào storage.
- `frontend/src/workspace.tsx`: Sidebar ứng viên còn bốn không gian; ba tab ngang cho CV riêng/builder/profile; tài khoản mở từ thẻ đáy và header; breadcrumb theo view. Cây Workspace được tạo lại theo danh tính đăng nhập hoặc trạng thái chờ xóa để hủy component của phiên trước.
- `frontend/src/main.tsx`: bọc các khu vực hiện có vào view; probe PDF nằm trong builder. Giữ state/refs/effects/handler nghiệp vụ và các form hiện có.
- `frontend/src/jobs.tsx`: một instance PublicJobBrowser tiếp tục sở hữu toàn bộ state tìm việc, CV ứng tuyển, submission keys, applications, lịch sử và notifications; đầu ra jobs và applications/notifications vào hai view riêng. Thông báo kết quả nộp đơn có mặt trong view jobs sau thao tác.
- `frontend/src/style.css`: thêm style có phạm vi cho outlet/tab/tài khoản và khoảng cách màn hình đơn; giữ các dòng CSS và biến màu hiện có.

Không thay dependency, backend, schema, provider config, dữ liệu hoặc bằng chứng E đã ghim. Thay đổi chưa commit trước lượt này được giữ; snapshot bốn file frontend trước sửa tại `.local/multiview-step1-20261028-215055/`.

## Kiểm chứng đã chạy

| Kiểm tra | Kết quả |
|---|---|
| `npm run typecheck` | Đạt |
| `npm run lint` | Đạt |
| `npm test` | 11/11: 4 test hiện có và 7 test ánh xạ route |
| `npm run build` | Đạt; Vite cảnh báo chunk React PDF lớn hơn 500 kB |
| Chromium / điều hướng | 48 assertions đạt: một view trong document, không tràn section, bốn mục Sidebar, ba tab, file input giữ cùng DOM node và file đã chọn, bản nháp/bộ lọc/form giữ nguyên, Back/Forward, reload, scroll/focus, skip-link, mobile 390 px |
| Chromium / vòng đời | 9 assertions đạt: polling writing hoàn tất khi view bị tháo, nội dung chưa duyệt giữ nguyên, revision guard vẫn hợp lệ, tạo PDF thật cục bộ, Blob URL và chữ ký `%PDF-` còn đọc được sau đổi view, logout/login hủy draft cũ |
| Chromium / vai trò | 12 assertions đạt: deep-link recruiter giữ query profile; draft recruiter/admin còn sau đổi view; không gắn view CV trái vai trò; pending deletion chỉ hiện recovery và không tải jobs/CV; guest/reset-token hoạt động |
| Đối chiếu source trước/sau | 7 kiểm tra đạt: block state/effects/handlers App và PublicJobBrowser, auth/profile/account logic, recruiter logic/JSX, toàn bộ opening tags input/select/textarea của main/jobs và mọi dòng CSS cũ giữ nguyên |
| `git diff --check` | Đạt; Git có thông báo chuẩn hóa LF/CRLF theo cấu hình repository |

Các lượt Chromium dùng Vite riêng ở 127.0.0.1:5186 và chặn toàn bộ `/api/**` bằng mock synthetic; không kết nối API/DB ứng dụng và không gọi provider thật. Navigation/preview tạo 0 API mutations. Lượt kiểm tra vòng đời gọi đúng hai mutation auth **giả lập** (logout/login). Đây là 69 browser assertions, không phải 69 bài E2E backend.

Script và ảnh tại `output/playwright/multiview-step1/`: `setup.cjs`, `check-navigation.cjs`, `check-lifecycle.cjs`, `check-roles.cjs`, `roles-result.json`, `source-preservation.json`, `applications-desktop.png`, `builder-mobile.png`. Đã xem hai ảnh desktop/mobile để kiểm tra bố cục của Bước 1.

## Phần chưa kiểm chứng và bước tiếp

Chưa chạy lại bộ E2E có backend thật/Fake worker. Các spec cũ đang giả định nhiều khu vực cùng xuất hiện trên trang; cần bổ sung bước điều hướng trong Bước 3, giữ assertion nghiệp vụ. Chưa kiểm chứng Firefox/WebKit. Hash reload giữ màn hình, không bổ sung khả năng phục hồi mọi bản nháp chưa lưu sau F5; cơ chế recovery bản upload dang dở hiện có của builder giữ nguyên.

Bước 2 sẽ thu gọn onboarding, tổ chức bộ lọc và jobs/builder đa cột, thêm empty state/CTA. Bước 3 sẽ cập nhật bộ E2E và chạy hồi quy tích hợp. Chưa commit/push/deploy hoặc thay đổi trạng thái nghiệm thu M6.
