# GĐ1 — Dữ liệu và kế hoạch đánh giá

> B/P09 đã có [evaluator trước sửa synthetic/Fake](../phase-03-extraction-contract.md#đánh-giá-trước-sửa) và [kết quả/giới hạn](../report/stage-b-2026-10-16.md). Bốn biến thể cùng một CV gốc, không phải quality corpus; JD có nhãn cấu trúc synthetic riêng. Không chuyển smoke thành final evaluation.

> Giai đoạn A đã bổ sung [giao thức/công cụ/manifest v0.1](stage-a-corpus-protocol.md) và [chuẩn bị provider](deepseek-probe-runbook.md). Chủ dự án xác nhận chưa có corpus được xác nhận quyền, dùng synthetic để phát triển; dành 28 giờ/tuần cho dữ liệu/gán nhãn. Không coi synthetic là bằng chứng chất lượng, không probe trả phí khi chưa được phép.

Trạng thái: evaluator L09 và bộ smoke giả đã chạy local ngày 16/10/2026; L12 thêm thống kê đối chiếu các revision được đánh dấu owner-confirmed; **chưa có nhãn giảng viên, dataset thật hay kết quả chất lượng AI**. Nguồn yêu cầu: [đặc tả](../../project-docs/01-system-specification.md), [roadmap](../../project-docs/03-roadmap.md).

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
| SYN-MATCH-SMOKE | [matching-baseline-smoke.json](matching-baseline-smoke.json) | vi/en, văn bản giả | group IDs giả danh | kiểm tra đường chạy evaluator và nhãn thưa; không phải evaluation |

Các test .NET còn sinh DOCX giả, ZIP quá mức và JSON sai trực tiếp từ mã nguồn. Không có JD thật, CV thật hoặc nhãn độ phù hợp; chưa đủ lập thực nghiệm hai chiều. Không báo tỷ lệ chính xác từ bộ này.

## Baseline L09 trên localhost

`GET /api/matching/jobs` nhận version CV của candidate hiện tại và chỉ xét tin `published`, chưa hết hạn, công ty chưa archive, owner còn active. CV có thể private; quyền dùng nó vẫn chỉ cho chính chủ. `GET /api/matching/public-resumes` yêu cầu recruiter owner của tin đang nhận hồ sơ và chỉ tải file cho các lựa chọn CV public hiện tại có source hash/file ready khớp; CV riêng đã nộp nhưng chưa public bị loại. Tên/headline chỉ có trong kết quả nếu trường đó được chia sẻ. Cả hai chiều không gọi provider và tính lại tại thời điểm request.

Scorer chuẩn hóa Unicode tiếng Việt không dấu và các token kỹ thuật được hỗ trợ, loại stop words cấu hình, dùng trọng số mặc định title=3, requirements=2, description=1. Mỗi token JD là duy nhất, field có trọng số cao hơn được giữ; giới hạn 500 token JD duy nhất mặc định. `retrieval_score` là tổng trọng số token trùng / tổng trọng số token JD, trong `[0,1]`; không có token JD hợp lệ trả null nội bộ, có query nhưng không overlap trả 0 và API không xếp vào danh sách. Hai chiều xếp cùng điểm cặp theo search-space tương ứng; không phải điểm 0–100 hoặc xác suất trúng tuyển.

Chạy smoke offline từ root PowerShell:

```powershell
./scripts/evaluate-matching-baseline.ps1
```

Script mặc định đọc bộ synthetic ở trên, không cần DB/API/provider, và ghi JSON vào `.local/evaluation/`. Dùng `-Dataset '<private dataset path>' -Output '<.local/evaluation/run.json>' -TopK 20` cho lần chạy riêng. Dataset có tối đa 1.000 CV, 1.000 JD, 10 MB; split chỉ nhận một tài liệu mỗi group để ngăn biến thể cùng người/tin rò qua split. Schema gồm `resumes`, `jobs`, `labels` với `relevance` 0–3; cặp không nằm trong labels giữ trạng thái chưa biết và không được tính là âm.

Schema dataset có thể thêm `confirmedRevision` cho CV/JD, gồm `origin`, `schemaVersion`, cờ `confirmedByOwner` và danh sách skills/requirements cùng provenance loại nguồn. L12 chỉ dùng chúng nếu revision là `user_confirmed`, schema hỗ trợ và confirmation được đánh dấu của owner; nếu thiếu hoặc sai, cặp bị thống kê là không đủ điều kiện, không chuyển sang dữ liệu AI chưa xác nhận. Recruiter/public view loại skills `user_statement`; candidate view có thể dùng chúng cho chính Candidate. Bộ evaluator thống kê số cặp đủ điều kiện và số yêu cầu có term overlap theo relevance, tách hai view. Đây là diagnostic/counts trên cặp đã gán nhãn, không phải ranking, precision/recall mới, score 0–100 hay đánh giá chất lượng; baseline L09 và metric cũ giữ nguyên. `confirmedByOwner` là metadata của file evaluation do người chuẩn bị dataset chịu trách nhiệm cung cấp, không phải chữ ký mật mã hoặc kết quả truy vấn DB. Các cờ trong bộ smoke chỉ là dữ liệu tổng hợp để kiểm tra nhánh code, không phải xác nhận của tài khoản thật.

Manifest ghi thời gian, split, top-k, số cặp/nhãn, hash input/config/assembly và Git revision, cùng ID giả danh, điểm top-k, độ phủ nhãn và nếu có thì tổng hợp revision diagnostics. Input text/projection không bị chép vào manifest; ID vẫn cần giả danh, không dùng email/tên hoặc thông tin liên hệ. Precision/nDCG chỉ tính cho query được gán nhãn đủ mọi cặp; nếu không có query như vậy, metric là null. Smoke mẫu có chủ ý nhãn thưa và một negative tường minh; metric nó in ra chỉ kiểm tra code, không được trình bày như phép đo mô hình.

API hiện chưa lưu kết quả baseline trong PostgreSQL và chưa cache; JSON evaluator là artifact local riêng. Chưa đo hiệu năng trên kho lớn hoặc quality trên tập thật; query recruiter hiện đọc toàn bộ CV công khai phù hợp ở local. Không có ngưỡng đạt được tự đặt.

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

Lưu run manifest: split/hash, commit code và assembly hash, model/provider revision nếu có, thời điểm, prompt/parser/schema/scorer config hash, cache bật/tắt, output IDs và lỗi. Baseline L09 đã được hiện thực trong lát local theo GĐ2; kết quả cache/video cũ phải gắn nhãn lần chạy cũ.

## Nội dung cần trao đổi với giảng viên

Quy mô/bộ ngành, mức hỗ trợ gán nhãn, rubric/trọng số/ngưỡng, top-k, chia tập và lịch đánh giá. Người thực hiện trực tiếp thu thập dữ liệu hợp lệ và trao đổi với giảng viên. Chưa liên hệ thay người dùng, chưa có phản hồi hoặc phê duyệt; không coi giảng viên là thành viên phát triển. Giới hạn dự kiến tập trung CNTT và Việt–Anh phải được báo trong thực nghiệm sau.
