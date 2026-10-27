# Bước 6 hoàn thành trong phạm vi thử nghiệm hẹp đã chốt

Ngày 27/10/2026, Asia/Saigon. Owner ủy quyền trực tiếp hoàn thành bước 6 sau quyết định local-only trước đó. Đã rà lại căn cứ nguồn, chốt quyền/phạm vi mới cho đúng sáu bản, hoàn thiện dữ liệu tham chiếu trong môi trường riêng, ghim phương pháp/giao thức/chi phí và kiểm khả năng thực thi. **Sáu điều kiện bước 6 đạt cho phạm vi đăng ký; bước 7/M5 và E chưa thực hiện.** Không nghiệm thu chất lượng đại diện hoặc tự đóng M3.

## 1. Sáu điều kiện đã hoàn tất

| Điều kiện | Bằng chứng hoàn thành |
|---|---|
| Ghim triển khai và cấu hình | 114 runtime files, 27 test sources, 10 components/23 method files, 2 DLL và 3 method config hashes khớp bước 5; không đổi thuật toán/prompt/rubric/scoring. |
| Chốt dữ liệu, nhãn, quyền và demo | 2 CV Anh × 4 JD, 6 groups; 131 field labels gồm 95 CV/36 JD; 8 relevance references/72 criterion judgments, 28 exclusions; local/provider metadata validator đều đạt. Demo mặc định dùng replay synthetic đã có, không gọi mới. |
| Audit độc lập theo phương pháp | Các nhóm không trùng tuning; chưa có output các phương pháp LLM/baseline trên bộ này được dùng tuning. Parser/JD có exposure lịch sử được giữ làm bổ trợ, không gọi final mới/pristine. Audit chốt riêng cho phương pháp chính, không bật claim đại diện hoặc coverage chưa có. |
| Giao thức/phép đo và bindings | k=3; version/revision/source/content/data hash và fingerprint 8 cặp, 4 writing payloads đã kiểm sau DB roundtrip. Các metric/denominator và xử lý lỗi giữ nguyên; input freeze tách với envelope kết quả chỉ lập sau khi E chạy. |
| Dự toán đủ và có trần | 14 jobs, tối đa 28 attempts, dự trù 0,20321280 USD; allocation tối đa 0,50 USD trong trần phát triển 2 USD. Tối đa 4 Tavily credits miễn phí/0 USD; email 0 mới. Đã đọc số dư/usage, giữ buffer cho chênh hiển thị; chưa chi/gọi generation hoặc search. |
| Bằng chứng provider và lịch sử | Tham chiếu writing/search/email thật đã có đúng phạm vi; giữ nguyên biên nhận/candidate cũ và quyết định local-only trước quyền mới, không gán provider run cũ cho DLL mới. |

Manifest: [final freeze](../evaluation/stage-d-step6-final-freeze-2026-10-27.json). [Protocol v4](../evaluation/stage-d-final-protocol-2026-10-27.md) và [dự toán cố định](../evaluation/stage-d-evaluation-budget-final-2026-10-27.json). Preflight trả `valid=true`, `ready=true`, `errors=[]`, `pending=[]`; đây là readiness của metadata/đầu vào, không phải điểm chất lượng hoặc quyền gọi từ chính công cụ.

## 2. Quyền và nguồn dữ liệu

Ủy quyền mới được ghi đúng lời owner trong receipt, tách với căn cứ của người thứ ba. Codex rà lại [CC0 của nguồn CV C](https://github.com/ar-nelson/resume/blob/b84fcd1549122d921ac141e1f563c424e5779044/LICENSE.md), [CC0 của nguồn CV V](https://github.com/veekas/react-resume/blob/cb99bb346cf78a71d852c211637c2643e60666b7/LICENSE), [DATA-LICENSE của Hiring-Bias](https://github.com/re-cinq/hiring-bias/blob/7559ef31da5c0ccfd56d373d488681ee88952e4c/DATA-LICENSE) và [MIT của VietJobs](https://github.com/VinNLP/VietJobs/blob/0ca6db907bf4db5332c90f5045b76911ba395a11/LICENSE). Notice, revision, attribution và license snapshots được giữ. Trên căn cứ này cùng ủy quyền thực hiện mới, đăng ký sử dụng DeepSeek cho đúng sáu bản nghề nghiệp/JD phục vụ private evaluation. Không suy quyền từ cờ local cũ hoặc mở raw CV/corpus khác. Đây không phải audit pháp lý độc lập hay bảo đảm ẩn danh hoàn toàn.

Corpus gốc 12 tài liệu và 55 frozen entries giữ nguyên, kể cả cờ transferfalse lịch sử. Quyền hiện hành nằm ở overlay/corpus-final mới; không sửa nhãn để giả đã được cấp quyền từ trước. 131 field labels và 8 pair references giữ nguyên bytes; actor Codex tự rà, human reviewed=0. Công cụ corpus chỉ kiểm khai báo/hash, không xác thực một lần human/legal review.

## 3. Dữ liệu ứng dụng và kiểm tra local

Tạo PostgreSQL riêng ở loopback 55442, database `careergap_eval`, không dùng DB ứng dụng. Nhập 2 CV revisions và 4 JD revisions tham chiếu do **Codex** xác nhận theo ủy quyền, không giả owner/tác giả thao tác UI hoặc đầu ra LLM. 106 facts giữ nguyên giá trị: 101 khớp nguyên văn parser spans, 5 giữ provenance `user_statement`. JD có 36 yêu cầu nguồn đã chọn, không là extraction exhaustive. Chưa chọn CV công khai.

Đã kiểm **8 pair bindings + 4 writing bindings** sau đọc DB, **20 negative controls** về sai actor/fact, và guard sai hash/quyền/template chưa có output. Các controls là dữ liệu dựng để kiểm hợp đồng, không model predictions hoặc điểm E. Parser admission lần lượt 6.457/3.714 units; pair tối đa 13.427, writing 9.801, đều dưới 16.000. Chỉ kiểm đọc/parser/provenance/admission; không dùng kết quả này làm chất lượng E. Ba positive source references không qua literal guard vẫn được giữ và báo giới hạn, không sửa gold.

Công cụ import riêng đã được sửa lỗi độ dài object key và thêm transaction; fixture dở được backup rồi dựng lại trong DB riêng. Không có sửa backend sản phẩm. Có một lỗi đặt tên metadata dự toán bị guard secret-field từ chối; đã đổi tên đúng nghĩa, giữ log lần thất bại và kiểm lại đạt. Bộ backend 28/28 và tool tests 61/61 là bằng chứng lịch sử đúng source/hash, không tuyên bố chạy mới trong lượt này hoặc trên PostgreSQL minor mới.

DB chuẩn bị dùng PostgreSQL 16.15; image ID, volume và dump private đã ghim. Container đã dừng, dữ liệu và bản sao giữ lại để E dùng; không có API/worker/provider được bật ở môi trường này. Source/DLL và file Word riêng của owner không đổi.

## 4. Tiền và điều kiện thực thi

Hai GET đọc tài khoản thành công: DeepSeek còn USD 4,99 và CNY 5,97, Tavily Researcher dùng 2/1.000, paygo usage 0. Giá được đối chiếu lại [DeepSeek pricing](https://api-docs.deepseek.com/quick_start/pricing/) ngày 27/10, vẫn input peak cache-miss 0,30/output 1,20 USD/million. Không gửi corpus trong hai GET này. API balance/usage không là hóa đơn; không xác nhận PAYG toggle mới từ giá trị paygo usage.

Chênh CNY 0,01 so với snapshot sau D chưa được gán nguyên nhân hoặc đổi sang USD. Giữ thêm **0,02 USD dự phòng**, không ghi thành chi thực mới. Ledger: đã chi bảo thủ 0,03 USD; remaining guarded 1,95 USD sau hold; allocation E 0,50 USD, phần chưa phân bổ 1,45 USD. Allocation active chặn dùng lại trần qua probe/DB/kỳ khác. Search 4 credits là allocation mới, không hoàn/reset 2 credits cũ; chỉ dùng khi free credits còn đủ, không bật PAYG/nâng gói. Demo live mới không nằm trong allocation; dùng replay đã có.

Trước E phải kiểm lại hash, giá/alias, số dư/credits và ledger, giữ nguyên các request/idempotency keys đã chuẩn bị; lỗi/unknown không tự retry vượt cap hoặc hoàn dự trù. Mẫu envelope kết quả chưa có output bị chặn đúng; sau E mới ghim hash baseline/captures vào envelope riêng, không đổi input lock để hợp thức hóa kết quả.

## 5. Giới hạn và bước kế tiếp

Chưa có CV native Việt, CV DOCX gốc hoặc JD Việt CNTT trong bộ này. Hai JD Việt ngoài CNTT không chứng minh matching CNTT tiếng Việt; CV reflowed không chứng minh khả năng đọc layout gốc. Mẫu nhỏ, labels Codex, nguồn/parser/JD đã có exposure và 28 criterion exclusions được báo riêng; chưa nghiệm thu chất lượng đại diện, M3 hoặc các claim thiếu coverage. Chiều JD→CV top-3 với pool 2 chọn cả pool, không đo selection loss. Không có gold score 0–100 hoặc ngưỡng giảng viên duyệt được tự tạo.

**Bước kế tiếp là bước 7: đối chiếu toàn bộ điều kiện M5 và lập biên bản riêng.** Bước 6 hoàn thành không tự nghiệm thu M5 hoặc mở E. Không cần owner lặp xác nhận quyền/hạn mức trong phạm vi đã ủy quyền. Lượt này: 0 generation, 0 search, 0 email, 0 phí provider mới; chỉ 2 GET đọc tài khoản và kiểm local. Không deploy/commit/push.
