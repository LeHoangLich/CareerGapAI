# Đối soát nguồn học lượt E — 31/10/2026

Auditor: **Codex (Authorized by Project Owner)**. Approved by: **Lê Hoàng Lịch (Project Owner)** — căn cứ chỉ thị ủy quyền đối soát, không phải xác nhận owner đã xem từng video. Review scope: `stage_e_vietnamese_learning_resources_5_criteria`. Status: `verified` đối với việc đối soát và bằng chứng ghi nhận; không có nghĩa cả năm tiêu chí đều đạt. Human authorized: true.

## Kết luận

Đã rà đủ ba kết quả và năm tiêu chí. **3/3 phù hợp Docker nhập môn theo tiêu đề/mô tả; 0/3 có bằng chứng nội dung gốc tiếng Việt.** Cả ba có tiêu đề, mô tả tiếng Anh và track phụ đề tự động `en/asr`; đây là bằng chứng hướng tới tiếng Anh, không phải tài liệu thuần Việt. Không kiểm chứng toàn bộ âm thanh hay bản dịch tự động.

Truy cập trang công khai không gửi cookie/tài khoản trả HTTP 200, đúng video ID và `playabilityStatus.status=OK` cho 3/3. Không thấy cổng bắt buộc trả phí/đăng nhập trong phản hồi trình phát này. Đây là kiểm tra HTML và metadata trình phát, **chưa trực tiếp phát video**, không bảo đảm khả năng xem ở mọi vùng/mọi thời điểm. Không đồng nhất xem công khai với giấy phép Open Access/tái sử dụng.

## Bảng đối soát

| Kết quả thực tế | Ngôn ngữ nội dung | Liên quan Docker gap | Trình độ beginner | Truy cập | Phí/đăng nhập |
|---|---|---|---|---|---|
| [Docker in 10 Minutes – Complete Beginner's Guide (2026)](https://www.youtube.com/watch?v=ZyWBs0CU2wk) | Tiêu đề/mô tả Anh; phụ đề tự động en; chưa xác nhận nội dung Việt | Khớp: containers/VM, Dockerfile, CLI, Compose, demo ứng dụng | Phù hợp nhập môn theo mô tả; chưa đánh giá chất lượng sư phạm. Tên “10 Minutes” không khớp timestamps kéo dài quá 20 phút | HTTP 200, đúng ID, player OK; chưa phát thực tế | Không có gate trả phí/login trong kiểm tra ẩn danh; không suy ra các tài nguyên liên kết ngoài cũng miễn phí |
| [Docker Tutorial for Beginners](https://www.youtube.com/watch?v=b0HMimUb4f0) | Tiêu đề/mô tả Anh; phụ đề tự động en; chưa xác nhận nội dung Việt | Khớp: images/containers, ports, volumes, Dockerfile, Compose | Bắt đầu nền tảng; có phần multistage/deploy nâng cao hơn, không mặc định mọi phần đều dễ cho người mới | HTTP 200, đúng ID, player OK; chưa phát thực tế | Không có gate trả phí/login trong kiểm tra ẩn danh. Tài trợ/Patreon được quảng bá không chứng minh video chính bị paywall |
| [Docker Crash Course for Absolute Beginners [NEW]](https://www.youtube.com/watch?v=pg19Z8LL06w) | Tiêu đề/mô tả Anh; phụ đề tự động en; chưa xác nhận nội dung Việt | Khớp: khái niệm Docker, cài đặt, images, registries, CLI, port binding, Dockerfile | Phù hợp nhập môn theo đề cương lý thuyết và demo | HTTP 200, đúng ID, player OK; chưa phát thực tế | Không có gate trả phí/login trong kiểm tra ẩn danh; các khóa/tài liệu quảng bá ngoài video chưa kiểm tra |

## Bằng chứng và phương pháp

- Đọc `search-docker-vi.private.json`: ba URL trên, `language=vi`, `languageBasis=query`, trạng thái succeeded; reason ghi chưa đọc toàn trang.
- Đọc `search-check.py`: request đã dùng `level=beginner`; không chạy lại script. `actual-learning-gaps.json` xác nhận Docker eligible; Git không có current gap. Không suy Docker gap thành ứng viên chắc chắn không có kỹ năng.
- `search-process.json` chỉ ghi tiến trình lịch sử với AI tắt; không coi PID cũ là trạng thái hiện tại.
- Đối chiếu `frontend/src/learning.tsx`: hiển thị **ngôn ngữ truy vấn**, kèm **Chưa xác minh ngôn ngữ toàn trang**. Không sửa mã nguồn.
- Mở trực tiếp các trang watch bằng HTTP GET thông thường, không API YouTube/Tavily, không dùng key/cookie, không đăng nhập. Đọc metadata nhúng trong HTML; không gọi endpoint player/caption, không tải video. Thời điểm kiểm tra lưu UTC trong receipt. Có một lần xuất console lỗi encoding; kiểm metadata đã chạy lại thành công và lưu UTF-8.
- Bằng chứng trước đây (hai 429/một shell) giữ nguyên lịch sử; kiểm tra 31/10 bổ sung bằng chứng truy cập mới, không sửa kết quả cũ.

## Ngân sách, bảo toàn và trạng thái

Ledger E giữ **2 requests/2 credits đã dùng**, allowance 0, trạng thái đóng; lần rà này **0 API calls, 0 search credits, 0 USD**. Hai credits E không phải tổng toàn tài khoản: dashboard owner 28/10 đã đối soát **4/1000 = D2 + E2**. `search-after.private.json` cũ ghi 2 là snapshot lịch sử, không ghi đè bằng số mới.

Biên nhận private: `.local/stage-e-followup-20261027/evidence/learning-source-audit-receipt.json`. Manifest bổ sung: `project-docs/evaluation/stage-e-learning-source-review-2026-10-31.json`. Hash trước/sau xác minh mã nguồn, ledger và artifact đầu vào giữ nguyên. Không thay retention policy; metadata rà soát mới không kéo dài thời hạn lưu dữ liệu nguồn.

**A13: hoàn thành đối soát kỹ thuật; giới hạn chất lượng nguồn tiếng Việt vẫn tồn tại.** Không cần owner tự click để hoàn thành việc đối soát được ủy quyền này. Không công nhận tiêu chí tài liệu tiếng Việt đạt, không thay bằng nguồn mới hoặc chạy search bổ sung. M6 và đánh giá ngoài giữ nguyên trạng thái.
