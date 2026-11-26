# Thiết kế lại giao diện tối giản, tĩnh — 28/10/2026

Theo yêu cầu trực tiếp của owner, áp dụng skill `design-taste-frontend` và ưu tiên phong cách tối giản, tĩnh hơn các mặc định motion của skill.

## Thay đổi

- Tách khung giao diện thành `Workspace`, `WorkspaceIntro`, `StaticLoading`; thanh điều hướng theo vai trò nằm bên trái ở desktop và thu gọn trên điện thoại.
- Nền sáng, một màu nhấn xanh trầm, Geist hỗ trợ tiếng Việt, icon Phosphor thống nhất. Không thêm animation, transition, carousel hoặc hiệu ứng tự chạy.
- Thiết kế lại bộ lọc, dòng tin tuyển dụng, trạng thái rỗng/đang tải/lỗi, biểu mẫu, quản lý CV, builder, đối chiếu, nguồn học và các bảng hiện có.
- Bổ sung liên kết tới đơn ứng tuyển/tài khoản, nút đăng nhập ở đầu trang, skip link và focus bàn phím. Các phần nghiệp vụ vẫn được mount để điều hướng không làm mất bản nháp.
- Hiển thị nhãn loại công việc/hình thức làm việc bằng tiếng Việt; giá trị gửi API giữ nguyên.
- Tailwind v4 qua PostCSS để dùng được cả cấu hình Vite gốc và launcher demo hiện có. Ghim dependency trong package/lockfile. Font giao diện đóng gói local; font và mã tạo PDF không đổi.

Không thay backend, schema, API, dữ liệu đánh giá, rubric, phương pháp AI, ledger hoặc quyền công khai CV. Các kết quả và gói source/frontend đã ghim trước đây vẫn thuộc phiên bản lịch sử; không cập nhật hash để gán giao diện mới cho phép đo cũ. Giữ các sửa đổi tài liệu/Word có sẵn của owner. M6 và nghiệm thu chất lượng đại diện giữ nguyên trạng thái.

## Kiểm chứng đã chạy

| Kiểm chứng | Kết quả |
| --- | --- |
| `npm run lint` tại `frontend/` | Đạt |
| `npm run typecheck` | Đạt |
| `npm test` | 4/4 đạt |
| `npm run build` | Đạt; cảnh báo kích thước chunk PDF lazy-load đã biết còn tồn tại |
| Bộ `frontend/e2e/` hiện có | 9/9 đạt, 52,1 giây |
| Hai vai trò tại 320 / 390 / 768 / 1024 / 1440 px | Không tràn ngang, không animation đang chạy |
| Điều hướng giữa builder và hồ sơ | Giữ dữ liệu bản nháp |
| Menu điện thoại | Mở, chọn mục, đóng đúng |
| Smoke bản build thật bằng preview local | Font Geist tải được, 0 request lỗi, 0 animation |

E2E dùng API DLL v4 hiện có, environment `Testing`, AI/search/email đều Fake và key trống. Tạo DB riêng `careergap_ui_redesign_20261028` bằng schema-only từ DB demo, không sao chép dữ liệu corpus/tài khoản. API 5094/Vite 5184 và preview 5185 chỉ phục vụ kiểm thử; dừng và dọn DB riêng sau kiểm chứng. Không chạy lại full backend suite vì không sửa backend; không gán 159/159 lịch sử thành lượt kiểm mới.

Log, tài khoản synthetic riêng và các kiểm tra responsive nằm trong `.local/ui-redesign-20261028/`; ảnh QA ở `output/playwright/`. Log Playwright xác nhận 9 bài đạt; bước in log của trình bọc ban đầu gặp lỗi encoding Windows sau khi bộ test kết thúc, đã sửa trình bọc. Không coi lỗi công cụ này là lỗi sản phẩm hoặc tuyên bố trình bọc ban đầu exit 0.

## Mở giao diện

Bản dùng thử hiện hành tại <http://127.0.0.1:5183/> đã nhận giao diện mới. Chỉ khởi động lại Vite để nhận cấu hình PostCSS, cập nhật PID trong receipt launcher; API/DB demo và dữ liệu người dùng giữ nguyên. Không deploy, commit, push hoặc gọi provider thật; không có phí mới.
