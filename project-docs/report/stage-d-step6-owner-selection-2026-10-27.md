# Bước 6 — Owner đã chọn bộ tài liệu cho chuẩn bị thử nghiệm hẹp

Ngày 27/10/2026, Asia/Saigon. Owner đã xác nhận dùng **2 CV Anh × 4 JD hiện có**, với các giới hạn đã nêu; **chưa cấp quyền gửi provider hoặc quyền chi API**. Việc 1 trong hướng dẫn owner hoàn thành. Việc tiếp theo là việc 2: xác nhận phạm vi quyền cho từng tài liệu.

Đã ghi biên nhận private, ghim đúng sáu ID/group/file-hash từ danh sách đã chuẩn bị và tạo [biên nhận metadata](../evaluation/stage-d-step6-owner-selection-2026-10-27.json) cùng [manifest chuẩn bị hiện hành](../evaluation/stage-d-step6-preparation-2026-10-27.json). Snapshot của 131 artifacts private, 10 artifacts public và 4 handoff trước đó đã được kiểm hash và giữ trước khi cập nhật tài liệu hiện hành. Không sửa manifest hoặc bằng chứng lịch sử để giả đã có quyết định từ đầu.

Phạm vi được chọn gồm hai CV Anh nghề nghiệp CNTT, hai JD Anh CNTT và hai JD Việt ngoài CNTT. Chưa đánh giá CV tiếng Việt hoặc matching CNTT tiếng Việt; ngôn ngữ và ngành chưa tách độc lập. Lịch sử tiếp xúc parser/JD được giữ theo từng phương pháp, không gọi toàn bộ bộ dữ liệu là tập end-to-end mới chưa chạm. Mục tiêu 4×4 vẫn có điều kiện, không là mức tối thiểu. Việc chọn bộ không đổi các nhãn Codex thành human gold, không tự miễn các điều kiện M3/M5/M6 còn thiếu.

Quyền của cả sáu tài liệu được chọn và metadata 13 tài liệu gồm tuning vẫn giữ `provider_transfer_allowed=false`; `api_calls_authorized=false`, không có allowance mới hoặc allocation/debit ngân sách. Trần 0,50 USD tiếp tục là dự toán đề xuất. Không đọc key, gọi provider, bật worker/API/DB hoặc tạo version/revision/confirmation trong ứng dụng.

Preflight trên manifest mới phải valid/pending, không ready; quyền, độc lập/phạm vi final, nhãn/bindings ứng dụng và funds/search còn chờ. Đây là ghi nhận lựa chọn và chuyển việc trong bước 6, **chưa final freeze/bước 6 hoàn thành/M5/E**. Không chạy lại suite hoặc provider cho cập nhật metadata này.

[Hướng dẫn owner — việc 2](../tasks/stage-d-step6-owner-actions-2026-10-26.md#2-xác-nhận-quyền-gửi-đúng-các-tài-liệu-được-chọn) có danh sách cụ thể và cách phản hồi; chỉ chuyển việc 3 sau khi việc 2 được giải quyết đúng phạm vi.
