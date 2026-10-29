# Multi-view SPA — Bước 2, 28/10/2026

Owner đã xác nhận kế hoạch và cấp quyền viết code Bước 2, tiếp tục dùng skill `design-taste-frontend`. Bước này tổ chức bố cục và trạng thái hiển thị trên kiến trúc cách ly view của Bước 1.

## Kết quả

- Onboarding đổi thành banner gọn với ba bước, mở rộng/thu gọn, ẩn và mở lại. Chỉ tùy chọn hiển thị được lưu trong localStorage theo vai trò; hoạt động trong bộ nhớ khi storage bị chặn. Link `#how-it-works` mở hướng dẫn. Không tạo chỉ số hoàn thành giả.
- Jobs có thanh tìm kiếm ngang, hai bộ lọc chính và năm bộ lọc nâng cao. Giữ nguyên markup input/select, giá trị, onChange và serializer query. Mở/đóng không gọi API; invalid event mở vùng nâng cao để native validation có thể focus trường lỗi.
- Workspace ứng viên dùng hai cột từ 1024px, cột trái 260–340px cho chọn CV và điều khiển matching; cột phải cho baseline, kết quả AI, tin công khai, phân trang và việc đã lưu. Dưới 1024px chuyển về một cột.
- `ResultPlacement` đưa output vào các đích portal ổn định ở cột phải. Một instance mỗi component tiếp tục giữ state/polling; không tạo bản desktop/mobile riêng. Giữ thứ tự baseline, cách chọn tập và thứ tự AI, topK, idempotency, nút tìm theo tiêu đề và các giới hạn chấm điểm. Các màn Recruiter dùng cách hiển thị inline mặc định.
- Builder chia 60%/40%: form và đề xuất cần duyệt bên trái; trạng thái, hướng dẫn, thao tác PDF, preview và probe bên phải. Hướng dẫn dài đầu trang thu gọn trong details, giữ nguyên nội dung. Nút tạo PDF bên ngoài form dùng thuộc tính `form` trỏ đến ID ổn định; vẫn chạy onSubmit hiện có. Probe không thuộc form builder.
- Panel chỉ sticky tại top 24px khi chiều cao đủ vừa viewport, tự chuyển về cuộn thường khi quá cao hoặc màn hình dưới 1024px. Hook chỉ theo dõi resize, không thêm scroll listener hoặc vòng render theo chuyển động chuột.
- Đơn/thông báo có bố cục hai cột desktop và empty states riêng. CTA sang `#jobs` giữ bộ lọc. Bổ sung state **hiển thị** loading/ready/error cho việc tải dữ liệu hiện có: chỉ báo trống sau khi tải thành công, có retry khi lỗi. Các endpoint và handler nộp/rút đơn, lịch sử, đánh dấu đã đọc giữ nguyên.

Giữ palette, font Geist, biến CSS và mọi dòng CSS có trước Bước 2; style mới bổ sung trong layer components. Không thêm dependency/backend/schema/config provider. Snapshot chín file trước sửa: `.local/multiview-step2-20261028-221159/`. Không sửa hoặc ghi đè artifact kiểm chứng Bước 1; ảnh hồi quy mới nằm trong thư mục Bước 2.

## Kiểm chứng

| Nhóm | Kết quả quan sát |
|---|---|
| Lint / typecheck / build | Đạt; typecheck cũng chạy trong build cuối. Cảnh báo chunk React PDF lớn hơn 500 kB vẫn còn |
| Unit tests | 11/11 đạt, gồm 7 route tests và 4 test API hiện có |
| Điều hướng | 48 assertions: một view trong document, giữ native file input và file đã chọn, bản nháp/profile/filter, Back/Forward/F5/skip-link/scroll, mobile |
| Vòng đời | 9 assertions: writing poll khi view bị tháo, giữ nội dung chưa duyệt/revision guard, PDF Blob còn đọc được sau đổi view, logout/login hủy draft cũ |
| Vai trò | 12 assertions: Recruiter/Admin, query profile, pending deletion, guest/reset link và không có mutation khi điều hướng |
| Bố cục và thao tác | 64 assertions: onboarding/storage, keyboard/validation, đủ 7 query filters, kết quả baseline/AI bên phải không nhân đôi, payload AI/topK, phân biệt chưa chấm, responsive, sticky, CTA, submit PDF bằng Enter, save guard và probe độc lập |
| Loading / error / populated | 11 assertions: không gọi lỗi tải là danh sách trống, retry thành công xóa lỗi, lịch sử/mark-read/rút đơn, rowVersion giữ nguyên, storage bị chặn, reduced motion |
| Đối chiếu source | 12 kiểm tra đạt: block nghiệp vụ builder/baseline/AI/handler mutation jobs không đổi; toàn bộ opening tags input/select/textarea của 5 file giữ nguyên theo multiset; writing và PersistentView byte-identical; CSS cũ là prefix nguyên vẹn |
| Diff | `git diff --check -- frontend/src` đạt; Git thông báo LF/CRLF theo cấu hình repository |

Tổng **144 browser assertions trong 5 nhóm**, không cộng các lượt chạy lại vào tổng. Kiểm tra bổ sung khi chụp ảnh xác nhận sidebar builder sticky ở y=24px; ở viewport 1366×768, workspace jobs bắt đầu khoảng y=479px khi banner thu gọn.

Các kích thước được kiểm tra: 320, 390, 768, 1024, 1366, 1440px; thêm viewport thấp 1366×300 và 683×384. Đây là kiểm tra vùng nhìn nhỏ, không phải bằng chứng đã thao tác browser zoom thực 200%.

Browser dùng Chromium qua Playwright CLI, Vite riêng tại 127.0.0.1:5186 và mock toàn bộ `/api/**`. Không gọi API backend/DB ứng dụng hoặc provider thật. Chỉ các thao tác được chủ động kiểm tra phát mutation **giả lập**: một admission matching, logout/login, mark-read/rút đơn. Điều hướng, mở/đóng giao diện, preview/probe không phát mutation dữ liệu/provider.

## Artifact và giới hạn

- Scripts, JSON kết quả từng nhóm, source checks, lint/build logs: `output/playwright/multiview-step2/`.
- Ảnh đã xem: `jobs-laptop.png`, `jobs-desktop-results.png`, `builder-laptop.png`, `builder-desktop.png`, `builder-mobile.png`, `applications-desktop.png`.
- PDF được tạo thật cục bộ; lượt vòng đời kiểm tra Blob URL và chữ ký `%PDF-`. Ảnh iframe trong Chromium headless không phải kiểm chứng hình thức nội dung PDF.
- Chưa chạy lại E2E tích hợp backend/PostgreSQL/Fake worker, chưa kiểm chứng Firefox/WebKit hoặc zoom trình duyệt thực. Bước 3 cần cập nhật navigation trong các spec cũ rồi chạy hồi quy tích hợp có kiểm soát; không giả định các assertion mock chứng minh backend ACL hoặc provider quality.
- Không thay dữ liệu/bằng chứng E, không gọi provider trả phí, không commit/push/deploy hoặc đổi trạng thái nghiệm thu M6.
