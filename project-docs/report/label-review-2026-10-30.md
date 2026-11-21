# Rà nhãn nguồn theo ủy quyền chủ dự án — 30/10/2026

Đã hoàn thành đối chiếu kỹ thuật 318 nhãn trường hiện hành bởi Codex, theo ủy quyền của Lê Hoàng Lịch (Project Owner). Chủ dự án phê duyệt trước quy trình và giao trách nhiệm thực thi; đây là bằng chứng ủy quyền, không phải bằng chứng chủ dự án đã trực tiếp đọc từng nhãn. Không có API bên ngoài, phí hoặc thay đổi runtime.

## Phạm vi và kết quả

| Bộ nhãn | Tài liệu | Nhóm nguồn | Nhãn trường | Validator offline |
| --- | ---: | ---: | ---: | --- |
| Final đã chốt 2 CV Anh × 4 JD | 6 | 6 | 131 | Đạt, hash nguồn khớp |
| Tuning, lấy bản adjudicated v2 mới nhất | 7 | 4 | 164 | Đạt, hash nguồn khớp |
| CV DOCX, lấy bản rà v2 mới nhất | 1 | 1 | 23 | Đạt, hash nguồn khớp |
| Tổng | 14 | 11 | 318 | Không lỗi schema/split/nhóm |

Đọc trực tiếp 5 PDF (12 trang đã render và xem), 1 DOCX qua OOXML và 8 JD text/Markdown. Toàn bộ 318 giá trị/trạng thái đã đối chiếu: 299 present, 18 absent trong đúng tài liệu được chọn, 1 ambiguous. Bản gốc và bản dẫn xuất cùng nhóm không được coi là hai nguồn độc lập. Phạm vi là các nhãn hiện có; không tuyên bố toàn bộ nội dung tài liệu đã được gán nhãn.

## Các điều chỉnh

1. Chuẩn hóa locator của 300 nhãn có trích dẫn (299 present và 1 ambiguous): thêm start/end/length, quote chính xác trong bản text trích xuất được ghim SHA-256, trang PDF và dòng text/đoạn OOXML. Tọa độ là Unicode codepoint, bắt đầu từ 0, end không bao gồm ký tự cuối; không phải byte offset hoặc UTF-16 runtime offset. Locator cũ lưu trong nhật ký before/after. Đây là nâng cấp độ chính xác và khả năng tái kiểm, không phải 300 lỗi giá trị.
2. Chọn đúng vị trí theo ngữ cảnh khi từ xuất hiện nhiều lần: Java không trỏ vào JavaScript; Git không trỏ vào Github; C không trỏ vào tên/liên hệ hoặc C++; các vai trò/ngày lặp gắn đúng kinh nghiệm; ngày giáo dục tách khỏi ngày kinh nghiệm; Rust của dự án tách khỏi Rust trong công việc. Employer The Knot dùng chung cho các vai trò tiếp theo được xác minh trên PDF.
3. Bổ sung `needsReview=true` và loại khỏi phép đo cấu trúc atomic chính xác cho ba nhãn:

| Tài liệu | Nhãn | Lý do |
| --- | --- | --- |
| jd-vi-05 | requirements[1] | Danh sách ngành học mở, có dấu `....`; không ép thành danh sách lựa chọn đóng |
| jd-en-01 | requirements[11] | Ví dụ trong ngoặc về machine learning không thành các điều kiện bắt buộc riêng |
| jd-en-01 | requirements[12] | Ví dụ dịch vụ AWS không thành các điều kiện bắt buộc riêng |

Giữ nguyên 318 giá trị/status và importance/minimumYears theo protocol hiện hành. `optional` của schema hiện hành được ánh xạ sang `preferred` ở metadata audit; không sửa enum của evaluator. Ghi heading nguồn cho từng nhãn JD. Giữ sửa importance `jd-vi-04/requirements[2]=required` từ bản v2 trước đây. Nhãn `jd-vi-04/requirements[1]` tiếp tục ambiguous do nguồn có cả kinh nghiệm từ một năm và không yêu cầu kinh nghiệm chuyên môn; không tự suy 0 hoặc 1 năm. Các grouping/shared-predicate chưa xác định vẫn có needsReview và danh sách loại trừ, không bị chuyển thành đúng chỉ để đóng công việc.

## Metadata và trạng thái nghiệm thu

Metadata `owner_authorized_review` ghi đầy đủ reviewer_name=Lê Hoàng Lịch, reviewer_role=Project Owner, technical_auditor=Codex (Authorized by Owner), reviewer=Lê Hoàng Lịch (Ủy quyền kỹ thuật: Codex), human_authorized=true, review_status=verified. Phạm vi verified là `owner_authorized_AI_source_audit`, actual_comparison_actor=Codex và human_direct_label_review_observed=false. Trường review_status cấp nhãn dùng `reviewed` đúng schema sẵn có; không sửa validator để hợp thức hóa enum mới.

Hạng mục **rà soát kỹ thuật theo ủy quyền đã hoàn thành**. Số nhãn được rà kỹ thuật theo ủy quyền là 318; số nhãn có bằng chứng người thật trực tiếp đối chiếu vẫn là 0. Không gán kết quả này thành human gold, review độc lập hoặc phản hồi giảng viên. M6 vẫn chờ; yêu cầu đánh giá ngoài tiếp tục hoãn theo quyết định chủ dự án.

## Toàn vẹn và tái lập

Tạo overlay mới trong `.local/label-review-20261030/`, không sửa bộ final/tuning lịch sử. Kiểm lại 22 file đầu vào gồm nguồn, manifest, field labels và pair references: hash không đổi. Split, group, quyền provider giữ nguyên. Validator kiểm cả 12 pair references về schema/hash/nhóm; đợt này không tái chấm ngữ nghĩa relevance của các cặp.

Ba lần CLI `scripts/validate-evaluation-corpus.py <manifest mới> --verify-files` đều đạt; kiểm bổ sung từng exact span/value/line, quyền, split và actor cho đủ 318 nhãn đạt. Không build hoặc chạy lại bộ test ứng dụng vì không sửa code runtime. Không tính lại metric E hoặc đổi kết luận lịch sử; nếu dùng overlay để đo tiếp phải ghi phiên bản nhãn mới và đây là rà sau thí nghiệm, không phải holdout mới.

## Hồ sơ

- [Manifest công khai](../evaluation/label-review-2026-10-30.json): số lượng, phạm vi, hash biên nhận.
- Biên nhận private: `.local/label-review-20261030/label-review-receipt.json`.
- Toàn bộ thay đổi before/after: `.local/label-review-20261030/label-adjustments.json`.
- Quyết định từng nhãn: `.local/label-review-20261030/per-label-decisions.json` và `label-review-index.md`.
- Ba manifest mới: `final-corpus-reviewed.json`, `tuning-corpus-reviewed.json`, `docx-corpus-reviewed.json`; các file nhãn JSONL đi cùng.
- Kiểm bổ sung: `independent-verification.json` (script kiểm riêng, không có nghĩa reviewer độc lập).

Không đưa nguồn CV/JD, liên hệ cá nhân hoặc toàn văn nhãn vào báo cáo public. Bộ bàn giao release30 và metric/ledger không thay đổi; báo cáo này là phụ lục rà nhãn mới.
