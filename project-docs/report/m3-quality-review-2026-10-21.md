# Biên bản kiểm tra nghiệm thu chất lượng M3 — 21/10/2026

> Lượt tiếp theo cùng ngày đã [bổ sung corpus pilot và nhãn dự thảo](m3-inputs-2026-10-21.md). Số liệu chưa có corpus ở biên bản kiểm tra này giữ theo thời điểm trước intake mới; M3 vẫn chưa đóng vì độ phủ và rà nhãn còn thiếu.

**Kết luận: chưa đủ bằng chứng để nghiệm thu chất lượng trên dữ liệu đại diện; M3 chưa đóng.** Chủ dự án yêu cầu tiếp tục nghiệm thu, thay cho việc tạm hoãn trước đó. Đã chạy lại đánh giá offline trên output DeepSeek gốc trước sửa và đối chiếu bằng chứng chức năng. Đây là kết luận kiểm tra, không phải biên bản công nhận đạt hoặc xác nhận của giảng viên.

Nguồn yêu cầu: [kế hoạch B/P04–P09](../tasks/CareerGapAI-danh-gia-va-ke-hoach-2026-10-16.md), [roadmap M3](../03-roadmap.md), [checklist](../phase-03-acceptance.md), [giao thức corpus](../evaluation/stage-a-corpus-protocol.md). Bản runtime được kiểm tra vẫn là `cca4378ea6752e078de389804a4ee64fdae11f75`; tài liệu đang có thay đổi chưa commit. Không đổi code, prompt, nhãn, split hoặc ngưỡng để làm đẹp kết quả.

## Kết quả thực hiện trong lượt nghiệm thu này

- Validator kiểm tra lại manifest, quyền khai báo, nhóm, nhãn và hash file: **hợp lệ, 6 CV/3 nhóm, 122 nhãn**; cảnh báo `synthetic_only_not_quality_evidence`. Kiểm tra này không xác minh độc lập giấy phép hoặc chất lượng nhãn.
- Replay 6 captures DeepSeek **trước người dùng sửa**: **TP 122, FP 0, FN 0; precision/recall/F1 = 1** trong phạm vi nhãn được chấm. Metrics, breakdown, method/prompt/parser/contract, hash dataset/captures và assembly khớp báo cáo trước. Không dùng child confirmed hoặc builder làm đáp án.
- Tiếng Việt: 3 file/61 trường; tiếng Anh: 3 file/61 trường. DOCX: 4 file/80 trường; PDF: 2 file/42 trường. Bản dịch và định dạng cùng nguồn được giữ chung nhóm, không coi là người độc lập.
- **Cả 122 nhãn là `present`, trạng thái `self_reviewed`; không có nhãn `absent`.** FP = 0 chưa chứng minh không bịa trên mọi field: prediction ở field chưa gán nhãn không được coi là negative. Chưa đo được chất lượng tránh bịa ở trường vắng trên corpus đại diện.
- Corpus live chỉ gồm synthetic: bốn bản của một nguồn CNTT và hai DOCX tiêu đề tự nhiên của nguồn kế toán/cơ khí. Chưa có PDF tự nhiên thuộc nguồn độc lập mới; chưa có CV đại diện không synthetic, tuning/final hoặc nhãn thực tế độc lập với kết quả model. Ba nguồn không chứng minh độ phủ nhiều ngành.
- JD có **3 mẫu synthetic/2 nhóm** trong `jd-atomic-smoke.json`, kiểm tra cấu trúc required/optional/any_of/số năm. Không có corpus JD đại diện; không suy các structural assertions thành precision/recall ngữ nghĩa của JD.

Bằng chứng mới private: `.local/m3-quality-20261021/corpus-check.json`, `replay.json`, `audit.json`. Audit lưu hash các đầu vào/bằng chứng đã đối chiếu. **Không có call provider mới, phí mới hoặc phép đo latency mới**; replay dùng lại output cũ. Không khởi động API/worker/DB, không thay đổi ledger hoặc mở lại probe đã đóng.

## Đối chiếu điều kiện M3

| Điều kiện | Bằng chứng | Kết luận trong phạm vi đã đo |
|---|---|---|
| Đọc CV/JD Việt–Anh, hiển thị xác nhận, xử lý file lỗi | CV v3/JD v2, parser và UI; báo cáo B ngày 16/19/21 | Đạt các ca local đã kiểm tra; coverage thực tế chưa có |
| Nguồn theo field; bổ sung của người dùng tách khỏi quote | Validator/child revision, real-output confirmation và raw bất biến | Đạt các ca đã kiểm tra; không thay kiểm tra ngữ nghĩa trên corpus đại diện |
| JSON/timeout/quota/restart không tạo thành công giả | Regression backend PostgreSQL **83/83** của lượt provider 21/10, completion/capture tests | Bằng chứng đã có; **không chạy lại suite trong lượt audit này**; chưa OS crash giữa call thật |
| simple-v1, lưu/mở/sửa/phục hồi, PDF ứng tuyển/snapshot | E2E và PDF QA trong báo cáo B; browser workflow dùng output thật ngày 21/10 | Đạt trong phạm vi local/synthetic đã kiểm tra; không cần thêm template |
| Đo extraction trước sửa và phân tích lỗi/giới hạn | Replay mới khớp 122/0/0; phân tích coverage ở trên | Đạt tái lập smoke; **chưa đủ nghiệm thu chất lượng đại diện** |
| Hợp đồng đầu vào đủ rõ cho rubric | Projection đúng scope, provenance, JD v2 và hợp đồng bàn giao trong checklist | Đã có hợp đồng dữ liệu. P10/C chốt rubric số và ngữ cảnh phân tích lại theo kế hoạch |

M3 trong roadmap yêu cầu đánh giá **ban đầu**, không yêu cầu tự đặt một mức F1 hoặc lấy xác nhận cuối của giảng viên làm chặn mọi phần local. Không thêm ngưỡng tùy ý. Tuy nhiên, yêu cầu đã chốt là synthetic không được dùng làm bằng chứng chất lượng thực tế; không thể đổi tên smoke thành corpus đại diện để công nhận đạt. Cũng không kéo việc hiện thực matching/rubric số của P10 vào B.

## Đầu vào còn thiếu để thực hiện nghiệm thu chất lượng

1. **Corpus CV/JD đại diện được phép dùng**: file gốc PDF có text/DOCX và JD Việt–Anh, nhóm nguồn độc lập, metadata ngành/layout; bằng chứng quyền đánh giá local. Chỉ đánh dấu quyền gửi DeepSeek khi phạm vi đồng ý/giấy phép cho phép. Không lấy CV công khai trên web làm mặc định có quyền; không dò kho người dùng, backup hoặc `.env` để tìm dữ liệu.
2. **Nhãn dựa trên bản gốc trước xem output**, có tự rà soát và ghi đúng người thực hiện. Gán cả trường có mặt và một tập trường vắng đã kiểm tra; giữ ambiguous/unreadable/pending riêng. JD cần nhãn nguyên tử, importance, số năm và quan hệ lựa chọn theo câu nguồn. Tách nhóm tuning/final trước chỉnh prompt; không chuyển các mẫu đã xem trong smoke thành final.
3. Khi có đầu vào: validate quyền/hash/split → trích xuất đúng phiên bản với ngân sách còn lại → lưu raw trước sửa → chấm và phân tích parser/model, vi/en, PDF/DOCX/JD, field thiếu/sai/nguồn → sửa trên tuning nếu cần → báo cáo trên tập giữ lại. Báo rõ giới hạn nếu số mẫu nhỏ; tiêu chí định lượng muốn dùng phải được ghi trước lần đánh giá tương ứng, không chọn sau khi thấy điểm.

Đã có [manifest/nhãn mẫu và hướng dẫn](../evaluation/stage-a-corpus-protocol.md), [capture/replay](../evaluation/cv-capture-evaluation.md) và [runbook provider](../evaluation/deepseek-probe-runbook.md); không tạo thêm một quy trình trùng lặp. Bản điền thật và dữ liệu cá nhân chỉ ở `.local`, không commit. **Không cần cung cấp lại key, ngân sách 2 USD, 28 giờ/tuần hoặc lựa chọn simple-v1.** Không tự quy định cỡ mẫu thành ngưỡng nghiệm thu đã được duyệt.

## Trạng thái sau kiểm tra

B chức năng/provider vẫn hoàn tất theo [report 21/10](stage-b-provider-2026-10-21.md). Yêu cầu nghiệm thu chất lượng đã được kiểm tra và ghi nhận **chờ đầu vào dữ liệu**, không còn chỉ ghi tạm hoãn theo yêu cầu cũ. Không khẳng định hệ thống trượt chất lượng thực tế khi chưa đo, cũng không công nhận đạt. Chưa bắt đầu C, không commit/push/deploy và không phát sinh chi phí mới.
