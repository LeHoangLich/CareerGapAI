# GĐ1 — Dữ liệu và kế hoạch đánh giá

Trạng thái: kế hoạch thử nghiệm, chưa có nhãn giảng viên hoặc kết quả chất lượng AI. Nguồn yêu cầu: [đặc tả](../../project-docs/01-system-specification.md), [roadmap](../../project-docs/03-roadmap.md).

## Danh mục ban đầu

Fixtures được tái tạo bằng [script](../../scripts/generate-fixtures.py), lưu `.local/fixtures/` và không commit. Nguồn: dữ liệu giả do agent tạo trong dự án cá nhân, cho phép sử dụng kiểm thử dự án; không lấy CV thật. ID nhóm dữ liệu giữ các định dạng của cùng nội dung chung split.

| ID | File sinh ra | Ngôn ngữ/định dạng | Group | Mục đích/split |
|---|---|---|---|---|
| SYN-VI-DOCX | synthetic-vi.docx | vi/DOCX, bảng | SYN-VI-01 | smoke/demo, không evaluation |
| SYN-VI-PDF | synthetic-vi-columns.pdf | vi/PDF text, hai cột | SYN-VI-01 | smoke/demo, không evaluation |
| SYN-EN-DOCX | synthetic-en.docx | en/DOCX, bảng | SYN-EN-01 | smoke/demo, không evaluation |
| SYN-EN-PDF | synthetic-en-columns.pdf | en/PDF text, hai cột | SYN-EN-01 | smoke/demo, không evaluation |
| SYN-NOTEXT-PDF | synthetic-no-text.pdf | PDF hình học, không text | SYN-NEG-01 | negative test; không phải ảnh scan thật |
| SYN-EXPORT-VI | PDF xuất từ UI | vi/en, 3 trang | SYN-EXPORT-01 | thử dấu/phân trang/copy; không mẫu CV sản phẩm |

Các test .NET còn sinh DOCX giả, ZIP quá mức và JSON sai trực tiếp từ mã nguồn. Không có JD thật, CV thật hoặc nhãn độ phù hợp; chưa đủ lập thực nghiệm hai chiều. Không báo tỷ lệ chính xác từ bộ này.

## Thu thập và nhãn

Mỗi tài liệu thật được phép dùng cần manifest: document_id, group_id (gồm bản dịch/định dạng/phiên bản), CV/JD, nguồn/URL nếu có, bằng chứng đồng ý/giấy phép, phạm vi dùng/lưu, ngành, ngôn ngữ, định dạng/bố cục, hash, vị trí lưu private, split và version nhãn. Không để PII trong manifest công khai. Không tự scrape CV.

Nhãn trích xuất: trường/giá trị kỳ vọng, trang/đoạn/quote, trạng thái thiếu/không đọc được; phân biệt thông tin trong tài liệu với bổ sung của người dùng. Chấm parser/LLM trước sửa, giữ output gốc và cấu hình phiên bản.

Nhãn cặp: CV group/JD group, yêu cầu JD và bắt buộc/ưu tiên, mức đáp ứng, bằng chứng, nhận xét mức liên quan. Các cặp chưa gán nhãn là chưa biết, không tự xem là negative. Bất đồng nhãn giữ cả nhận xét, nhờ người đánh giá thống nhất; chưa nhận hỗ trợ thì ghi chưa xác nhận.

Chia tập theo nhóm gốc trước điều chỉnh prompt/rubric: mọi bản dịch/file/version cùng CV/JD giữ cùng nhóm; không để cùng tài liệu xuất hiện giữa tuning và evaluation. Demo tách riêng. Tỷ lệ và số lượng chốt khi có dữ liệu, không tự đặt con số đã được duyệt. Đánh giá cuối đóng băng manifest/nhãn/method trước chạy.

## Các phép đo dự kiến

- Trích xuất: precision/recall/F1 trường có nhãn, tỷ lệ đọc được, lỗi nguồn/quote; phân tách vi/en, PDF/DOCX và bố cục; đo trước xác nhận của ứng viên.
- Matching: baseline từ khóa và phương pháp LLM trên cùng split, cả CV→JD và JD→CV. Đo chọn tập (recall của cặp phù hợp có nhãn) riêng với thứ tự top-k (precision/nDCG khi có nhãn đủ); ghi rõ độ phủ nhãn và k đã chọn cho đợt thử.
- Điểm/giải thích: so rubric với người đánh giá, thống kê claim không có bằng chứng, yêu cầu bắt buộc bị che khuất và độ ổn định lặp cùng đầu vào/phương pháp.
- Nguồn học: URL thực, ngôn ngữ/nguồn/mức liên quan và thông tin được snippet hỗ trợ; không nhận đã đọc cả trang.
- Vận hành: độ trễ, token/credits, retries/outcome_unknown, chi phí, bộ nhớ; không suy từ API trả 200 thành chất lượng tốt.

Lưu run manifest: split/hash, commit code, model/provider revision nếu có, thời điểm, prompt/parser/schema/scorer config hash, cache bật/tắt, output IDs và lỗi. Baseline chưa hiện thực trong GĐ1 (theo GĐ2); kết quả cache/video phải gắn nhãn lần chạy cũ.

## Nội dung cần trao đổi với giảng viên

Quy mô/bộ ngành, mức hỗ trợ gán nhãn, rubric/trọng số/ngưỡng, top-k, chia tập và lịch đánh giá. Người thực hiện trực tiếp thu thập dữ liệu hợp lệ và trao đổi với giảng viên. Chưa liên hệ thay người dùng, chưa có phản hồi hoặc phê duyệt; không coi giảng viên là thành viên phát triển. Giới hạn dự kiến tập trung CNTT và Việt–Anh phải được báo trong thực nghiệm sau.
