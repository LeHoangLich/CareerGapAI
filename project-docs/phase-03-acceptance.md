# Checklist M3 và đầu vào bàn giao sang C

Cập nhật 19/10/2026. B/P04–P09 local có implementation/bằng chứng; **M3 tổng thể còn chờ provider/corpus và đánh giá thực tế**. [Báo cáo 16/10](report/stage-b-2026-10-16.md) giữ lịch sử; [bổ sung 19/10](report/stage-b-followup-2026-10-19.md) ghi lượt mới. Không cộng số test giữa các lượt.

| Điều kiện | Đã có local | Còn chờ nghiệm thu tổng thể |
|---|---|---|
| CV/JD Việt–Anh có cấu trúc và nguồn, không bịa field thiếu | CV v3/quote validator, JD v2/labeled synthetic, owner review/child revision | Corpus có quyền/nhãn đại diện; đo sai field/ngữ nghĩa/nguồn/layout |
| File khó/lỗi có trạng thái | Parser child deadline, PDF/DOCX/no-text/broken/header/footer/table; replay tách parser/output | Coverage file thực tế, không hứa mọi layout/OCR |
| Worker/usage/trạng thái đúng | Retry/timeout/quota/lease regression, host replacement skills/full, truncated JSON bị chặn/settle known usage | Model/giá/key/số dư/kỳ/cap và quyền probe; chưa OS crash trong call thật |
| Tạo/mở/sửa/phục hồi/PDF ứng tuyển | simple-v1 đã chốt, recovery/session notice, PDF→apply→snapshot, text/render/geometry | Luồng local không chờ mẫu thứ hai, hosting hoặc DeepSeek |
| Đánh giá trước sửa | Manifest/nhãn độc lập, owner export raw, assembler/replay, breakdown/error/null metrics | Chạy model thật trên tài liệu có quyền; phân tích lỗi/usage và sửa theo bằng chứng |
| Đầu vào rubric | Projection theo scope, JD required/optional/years/any_of, hợp đồng dưới đây | P10/C chốt rubric thử nghiệm/version và JD snapshot/hiện tại khi phân tích lại |

## Hợp đồng đầu vào sang C — dự thảo, chưa là rubric điểm

- Ghim CV/JD version và revision cụ thể, lưu fingerprint. Không dùng latest revision backfill application snapshot. Private/self, public và application là các scope quyền khác nhau.
- Self có thể dùng user_statement nhưng gắn nhãn; public diagnostic chỉ document spans, builder v2 không tự thành quote file. AI không đổi trạng thái đơn.
- Giữ JD importance `required/optional/unspecified`, `minimumYears` tường minh, `single/any_of`. `needsReview` chưa giải quyết không âm thầm biến thành hai bắt buộc. Period CV không tự chứng minh số năm một skill; không cộng chồng lấn/infer từ chức danh.
- Kỹ năng, kinh nghiệm/dự án, học vấn/chứng chỉ và điều kiện tuyển dụng là các nhóm dữ kiện có thể đối chiếu, chưa phải trọng số chốt. Alias diagnostic có version, không gộp Java/JavaScript hoặc SQL/PostgreSQL.
- Vắng field → **chưa có bằng chứng**; unreadable → lỗi/không đủ dữ liệu; chưa nhãn → unknown. Không đổi thành thiếu năng lực, điểm 0 hay tự loại.
- L09 retrieval_score 0–1 vẫn baseline; counts diagnostic không phải 0–100. P10 chốt trọng số/partial/no_evidence/required và ngữ cảnh JD. B không tự đặt ngưỡng, làm match_pair/AI viết CV/nguồn học.

## Ranh giới phần chờ

Đã chốt 28 giờ/tuần cho dữ liệu, một simple-v1, chưa corpus có quyền và DeepSeek cung cấp sau; không hỏi lại hoặc tự mua dịch vụ. Công cụ local dùng synthetic; không đổi nhãn thành chất lượng thật.

Sau khi có dữ liệu/provider: kiểm tra quyền/cap → probe v3 ngắn → raw trước sửa → đánh giá tách parser/model và coverage → sửa theo bằng chứng → biên bản nghiệm thu đúng phiên bản. B local không tự đóng điều kiện M1/M2. Roadmap có ý thử AI viết sớm, nhưng kế hoạch tuần tự hiện hành đặt implementation P16/D; không tự triển khai trong B.
