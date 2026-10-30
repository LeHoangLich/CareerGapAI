# Xem lại kết quả CV–JD sau diễn tập — 30/10/2026

Theo yêu cầu của chủ dự án, **tạm hoãn việc yêu cầu giảng viên/người đánh giá thật dùng thử và nhận xét** cho đến khi chủ dự án mở lại. M6 vẫn chờ; không chuyển diễn tập AI thành phản hồi người thật hoặc tự đổi điều kiện nghiệm thu.

## Công việc kỹ thuật đã hoàn thành

Diễn tập ngày 30/10 phải sửa idempotency key bằng script để mở lại kết quả matching. Đã bổ sung nút **Xem kết quả đã lưu** tại từng cặp CV–JD: người dùng tìm lại đúng CV/version và tin, bấm nút để đọc lần phân tích gần nhất của chính tài khoản trong đúng ngữ cảnh. Không cần biết job ID hoặc can thiệp request.

- API mới: `GET /api/matching/pairs/latest?resumeVersionId=...&jobVersionId=...&context=self|public_pool|application&applicationId=...`. `applicationId` chỉ có trong ngữ cảnh đơn ứng tuyển. Phản hồi `{ pair: ... }`, hoặc `{ pair: null }` khi không có kết quả được phép xem; ngữ cảnh không hợp lệ trả 400.
- Truy vấn lọc requester, CV version, JD version, context và application ID, chọn job gần nhất. Dùng lại kiểm tra quyền hiện tại trước khi trả dữ liệu; đọc chi tiết kiểm tra lại quyền sau các lần đọc DB. Không trả kết quả tài khoản khác hoặc public CV đã thu hồi quyền. Ngữ cảnh application giữ CV/JD và revision phù hợp lúc nộp.
- Xem lại không gọi enqueue, không tạo job/attempt hoặc đặt trước ngân sách. Đọc kết quả đã lưu vẫn dùng được khi AI bị tắt. Job đang chạy tiếp tục được theo dõi; job lỗi giữ trạng thái lỗi, không sinh điểm 0 giả.
- UI ghi rõ kết quả thuộc dữ liệu/phương pháp của lần phân tích trước; đây không phải tự đánh giá lại dữ liệu hiện tại. Khi đổi cặp/ngữ cảnh, trạng thái đọc cũ được bỏ. Nút tạo phân tích mới vẫn là thao tác riêng.

Phạm vi là mở **lần phân tích gần nhất**, không phải danh sách toàn bộ lịch sử. Quyền đọc vẫn có thể mất khi CV/JD/tài khoản không còn đủ điều kiện. Không đổi rubric, prompt, thuật toán baseline, worker, phương pháp AI, schema hoặc corpus đánh giá.

## Kiểm tra đã chạy

Lệnh: `python scripts/test-frontend-regression.py --tests writing-matching.spec.ts`.

| Kiểm tra | Kết quả mới |
| --- | --- |
| Kiểm tra công cụ chạy | 6/6 đạt |
| Frontend lint/typecheck/build | Đạt |
| Frontend unit | 11/11 đạt |
| Backend integration/unit với PostgreSQL | 160/160 đạt, không bỏ qua |
| E2E writing–matching–application | 1/1 đạt, không sửa request/giả response |

Backend kiểm tra không có kết quả, queued/succeeded, chọn job gần nhất, ngữ cảnh sai, sai tài khoản/role, không tạo thêm job/attempt khi đọc lặp và khi AI tắt; bổ sung đối soát thu hồi public CV và application giữ JD/revision cũ sau cập nhật JD. E2E dùng frontend/API/worker Fake thật local: tạo CV và JD synthetic, phân tích, reload toàn trang, tìm lại cặp, bấm nút đọc, mở tiêu chí rồi tìm nguồn học Fake. Kết quả thuộc đúng job trước đó, số POST tạo matching/recommendation trong pha xem lại bằng 0; đơn ứng tuyển sau đó vẫn tải đúng bytes PDF đã nộp.

Bằng chứng private: `.local/frontend-step3-20261030-110603-1f9e/` gồm `result.json`, `results/backend.trx`, `e2e.json`, logs và source hashes. Run completed/cleanup đều true; container PostgreSQL tạm đã dọn. Audit DB E2E: non-Fake methods 0, USD budgets 0; chỉ Fake/test_credit spent 2, reserved 0. Không đọc `.env` hoặc gọi DeepSeek/Tavily/Resend thật.

[Manifest kiểm chứng](../evaluation/pair-history-followup-2026-10-30.json).

## Ranh giới phiên bản và bước tiếp theo

Đây là sửa mã nguồn sau bộ bàn giao ngày 29/10, được build/test trên runtime và DB tạm riêng. **Chưa cập nhật runtime demo 5190, source ZIP, Docker image, slide hoặc video đã chốt.** Không gán các test mới cho binary demo cũ, và không gán kết quả chất lượng provider cũ cho DLL mới. Không commit/push/deploy trong công việc này. Phiếu Word và các thay đổi riêng của người dùng không sửa.

Kết quả chất lượng ngày 27/10, giới hạn tập đã xem, human benchmark labels 0 và các ledger thật giữ nguyên. Phần kỹ thuật tiếp theo nếu tiếp tục là đồng bộ một revision demo/bàn giao với sửa này và kiểm tra trên revision đó; các gói cũ giữ lịch sử. Các đề xuất dịch thuật, stop-words và quy định xem tin đóng chưa được tự mở rộng hoặc đổi theo diễn tập.

Phần giảng viên/người đánh giá thật được hoãn theo yêu cầu, không nhắc lại như công việc bắt buộc phải làm ngay. [Ma trận M6](../handoff/stage-e-20261029/nghiem-thu-M6.md) vẫn là điều kiện nghiệm thu; mở lại phần đánh giá khi chủ dự án yêu cầu.
