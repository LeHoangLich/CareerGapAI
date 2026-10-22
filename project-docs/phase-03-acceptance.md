# Checklist M3 và đầu vào bàn giao sang C

Cập nhật cuối ngày 21/10/2026: [biên bản kiểm chứng local mới](report/m3-local-acceptance-2026-10-21.md). **94/94 backend PostgreSQL, 9/9 E2E**, PDF 2/1 trang đạt; corpus **6 CV/4 nguồn + 8 JD/6 nhóm, 292 nhãn dự thảo** đã có DOCX gốc. Parser đọc 6/6, khôi phục nguyên văn 205/205 giá trị đã nhãn; JD parser v3 được sửa/đo trước sửa trên tuning và holdout. **Chức năng local đạt các ca đã chạy; M3 chất lượng đại diện chưa đóng.** Không có call/phí DeepSeek mới, không triển khai C.

Lịch sử: [intake v1 12 tài liệu](report/m3-inputs-2026-10-21.md), [kiểm tra trước intake](report/m3-quality-review-2026-10-21.md), [probe DeepSeek synthetic](report/stage-b-provider-2026-10-21.md), [B ngày 16/10](report/stage-b-2026-10-16.md), [bổ sung 19/10](report/stage-b-followup-2026-10-19.md). Không cộng số test hoặc gán điều kiện thiếu của lượt cũ cho hiện trạng mới.

| Điều kiện | Đã có local | Còn chờ nghiệm thu tổng thể |
|---|---|---|
| CV/JD Việt–Anh có cấu trúc và nguồn, không bịa field thiếu | CV schema 3, JD schema 2/parser 3, owner review/child revision, quote/offset checks | CV native Việt còn thiếu; nhãn do Codex, chưa người rà và chưa đủ độ phủ đại diện |
| File khó/lỗi có trạng thái | Deadline/kill tree, 94 regression; đọc 6 file ngoài synthetic có native DOCX/PDF, 205 giá trị nhãn khôi phục | Đây là parser value recovery, không phải semantic LLM quality hoặc chứng minh mọi layout/OCR |
| Worker/usage/trạng thái đúng | Retry/timeout/quota/lease/completion regression, synthetic DeepSeek capture replay | Provider thật đã qua probe synthetic; chưa OS crash trong call thật, không tạo phí để giả lập |
| Tạo/mở/sửa/phục hồi/PDF ứng tuyển | 9 E2E mới đạt, PDF dài/ngắn render/text/geometry/counter đạt, simple-v1 | Đạt phạm vi local/synthetic đã chạy, không chờ mẫu khác/hosting |
| Đánh giá trước sửa | Raw/replay synthetic, real-source parser + JD v2/v3 tuning/holdout diagnostics, error analysis và hash freeze | Chưa DeepSeek trên corpus ngoài synthetic: transfer false; cần quyền phù hợp, người rà nhãn và native vi CV. Không gọi span coverage là semantic F1 |
| Đầu vào rubric | Projection theo scope, JD required/optional/years/any_of, hợp đồng dưới đây | P10/C chốt rubric thử nghiệm/version và JD snapshot/hiện tại khi phân tích lại |

## Hợp đồng đầu vào sang C — dự thảo, chưa là rubric điểm

- Ghim CV/JD version và revision cụ thể, lưu fingerprint. Không dùng latest revision backfill application snapshot. Private/self, public và application là các scope quyền khác nhau.
- Self có thể dùng user_statement nhưng gắn nhãn; public diagnostic chỉ document spans, builder v2 không tự thành quote file. AI không đổi trạng thái đơn.
- Giữ JD importance `required/optional/unspecified`, `minimumYears` tường minh, `single/any_of`. `needsReview` chưa giải quyết không âm thầm biến thành hai bắt buộc. Period CV không tự chứng minh số năm một skill; không cộng chồng lấn/infer từ chức danh.
- Kỹ năng, kinh nghiệm/dự án, học vấn/chứng chỉ và điều kiện tuyển dụng là các nhóm dữ kiện có thể đối chiếu, chưa phải trọng số chốt. Alias diagnostic có version, không gộp Java/JavaScript hoặc SQL/PostgreSQL.
- Vắng field → **chưa có bằng chứng**; unreadable → lỗi/không đủ dữ liệu; chưa nhãn → unknown. Không đổi thành thiếu năng lực, điểm 0 hay tự loại.
- L09 retrieval_score 0–1 vẫn baseline; counts diagnostic không phải 0–100. P10 chốt trọng số/partial/no_evidence/required và ngữ cảnh JD. B không tự đặt ngưỡng, làm match_pair/AI viết CV/nguồn học.

## Ranh giới phần chờ

Đã chốt 28 giờ/tuần, một simple-v1 và key/trần DeepSeek 2 USD cho toàn giai đoạn phát triển; không hỏi lại hoặc tự mua dịch vụ. Đã có pilot 14 tài liệu/292 nhãn, kể cả DOCX gốc; không còn thiếu toàn bộ corpus. Còn CV native Việt có quyền, người rà nhãn và quyền gửi đúng corpus ngoài synthetic cho provider. Không tự ký duyệt, cấp quyền thay người sở hữu hoặc biến nhãn Codex thành human/expert reviewed.

Luồng provider đã kiểm chứng trên synthetic. Sau khi có corpus đại diện: kiểm tra quyền/cap → probe v3 ngắn → raw trước sửa → đánh giá tách parser/model và coverage → sửa theo bằng chứng → biên bản nghiệm thu đúng phiên bản. B local không tự đóng điều kiện M1/M2. Roadmap có ý thử AI viết sớm, nhưng kế hoạch tuần tự hiện hành đặt implementation P16/D; không tự triển khai trong B.
