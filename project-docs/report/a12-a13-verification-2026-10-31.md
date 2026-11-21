# Tái kiểm A12 và rà chức năng A13 — 31/10/2026

Auditor: **Codex (Authorized by Project Owner)**. Approved by: **Lê Hoàng Lịch (Project Owner)** theo chỉ thị thực thi; không gán owner là người trực tiếp chạy test. Status: **verified**. Human authorized: **true**.

## A12 — đạt kiểm thử tự động

Pipeline mới `20261031-172834-d216`: backend PostgreSQL **160/160**, frontend unit **15/15**, toàn bộ E2E **22/22**; lint, typecheck, build đạt. Dọn môi trường test hoàn thành. Token rỗng/sai/hết hạn/đã dùng và mật khẩu không đủ độ dài được kiểm ở backend; E2E dùng link đã dùng để kiểm thông báo và chuyển forgot/gửi yêu cầu mới. Không tuyên bố E2E đã chờ hết hạn. Vite có cảnh báo kích thước bundle không chặn build.

Bằng chứng/hash chi tiết tại [manifest A12](../evaluation/password-reset-feedback-2026-10-31.json); [biên bản A12](password-reset-feedback-2026-10-31.md) đã cập nhật, kết quả lượt trước giữ nguyên lịch sử.

## A13 — chưa phát hiện lỗi chức năng trong phạm vi rà

Đối chiếu ba URL của artifact E với `TavilySearchClient.cs`, `LearningService.cs`, `learning.tsx` và tests hiện có:

- Adapter tạo query Docker + beginner + tiếng Việt, Basic, max_results=3; không hứa xác minh ngôn ngữ video. URL/tiêu đề lấy từ provider, có kiểm HTTPS/public-link, giới hạn kích thước và loại trùng.
- Service kiểm topic/gap và tham số, giữ ngôn ngữ là ngôn ngữ request; trả `languageBasis=query`, không nâng metadata thành ngôn ngữ nội dung đã xác minh.
- UI ghi đúng “ngôn ngữ truy vấn” và “Chưa xác minh ngôn ngữ toàn trang”; có xử lý lỗi và trạng thái busy. Service xử lý HTTP/cancellation/JSON/integration errors thành lỗi có kiểm soát, giữ reservation cho outcome_unknown.
- Bộ backend mới gồm SearchAdapterTests dùng HttpMessageHandler giả, LearningTests về quyền/cache/quota/unknown outcome. E2E mới chạy lại luồng nguồn học Fake từ kết quả cặp đã lưu. Không gọi provider để kiểm adapter.
- Kết quả trang công khai HTTP200/player OK và caption en của lần đối soát trước được dùng làm bằng chứng đã lưu, **không mở lại URL** trong lượt này. Không biến kiểm metadata thành xác nhận phát video toàn bộ hoặc mọi tài nguyên liên kết ngoài miễn phí.

Không phát hiện lỗi code giải thích việc ba kết quả tiếng Anh xuất hiện: đây là giới hạn kết quả provider và chất lượng ngôn ngữ, đã được UI công khai. **Giữ nguyên adapter/UI**, không thêm bộ lọc ngôn ngữ hoặc đổi logic ngoài phạm vi. Việc rà năm tiêu chí đã xong; chưa có bằng chứng nguồn thuần Việt đạt. Kết luận giới hạn ở luồng đã rà và test, không phải cam kết toàn hệ thống không còn bug.

## Bảo toàn và tiến độ

126 file nguồn/ledger đối chiếu hash không đổi. Lượt này 0 external API calls, 0 credits, 0 USD; không thay retention, corpus, metric, demo5190 hoặc gói bàn giao. M6/P20 vẫn chờ đánh giá ngoài đang hoãn. Không tăng phần trăm tiến độ theo việc tái chạy test. [Manifest audit](../evaluation/a12-a13-verification-2026-10-31.json).
