# Giao thức cố định cho thử nghiệm hẹp sau bước 6

Phiên bản `stage-d-final-protocol-v4-scoped-freeze`, ngày 27/10/2026, Asia/Saigon. Kế thừa thuật toán, prompt, rubric, phép đo và quy tắc loại trừ của [v3](stage-d-final-protocol-2026-10-26.md). Bản này chốt dữ liệu, quyền, bindings và kế hoạch thực thi theo ủy quyền mới của owner; không đổi phương pháp để làm kết quả tốt hơn. Bước 6 chuẩn bị đánh giá; chưa chạy đánh giá E hoặc nghiệm thu M5/M3.

## 1. Phạm vi và tính độc lập theo phương pháp

Bộ đã chọn: **2 CV Anh ngành CNTT × 4 JD**, gồm 2 JD Anh CNTT và 2 JD Việt ngoài CNTT; 6 nhóm nguồn độc lập, không trùng các nhóm tuning. `top-k=3`. Mục tiêu 4×4 là có điều kiện, không phải minimum. Không dùng synthetic, bản dịch hoặc biến thể cùng nhóm để bù số lượng.

Phạm vi thực nghiệm chính được đăng ký:

| Thành phần | Tập và điều kiện |
|---|---|
| CV semantic extraction | Hai PDF nghề nghiệp, 95 field references; chưa có output DeepSeek trên hai CV này được dùng tinh chỉnh phương pháp. Upstream parser đã được xem; không gọi là end-to-end pristine. |
| Baseline và pair | Ma trận 2×4 đầy đủ, 8 relevance references và 72 criterion judgments; 28 judgments được loại theo quy tắc nguồn đã ghim. Không có output matching trên các nhóm này được dùng tuning. |
| Writing | Hai CV, mỗi CV một đề xuất nhãn Việt và một nhãn Anh; kiểm nguyên dữ kiện và nhóm. Nhãn ngôn ngữ không dịch facts. |
| Learning | Đối chiếu gap với packet topic độc lập đã ghim; chỉ search nếu gap thật hỗ trợ chủ đề tương ứng. Tối đa hai topic Docker/Git × vi/en, tổng 4 credits miễn phí. Không có gap phù hợp thì ghi không có lượt/coverage, không dựng URL. |

Parser/JD có output lịch sử đã xem ngày 21/10; giữ làm replication/diagnostics bổ trợ, **không dùng thay một final holdout mới của parser/JD**. Hai parser captures tạo ở bước 6 chỉ phục vụ import/provenance/admission; không phải kết quả chất lượng E. Audit và routing mới ghi rõ từng thành phần, không đổi lịch sử exposure hoặc biến nhãn Codex thành human gold.

CV gốc Việt, DOCX gốc và matching CNTT tiếng Việt chưa được đánh giá bằng bộ này. Hai JD Việt ngoài CNTT gây nhiễu ngôn ngữ–ngành; không suy kết luận hiệu ứng ngôn ngữ riêng. Các CV là bản nghề nghiệp dẫn xuất, không đại diện bố cục và thông tin của CV đầy đủ. Cỡ mẫu nhỏ, không có ngưỡng đại diện hoặc ngưỡng giảng viên đã duyệt được giả lập. Những claim thiếu coverage tiếp tục chưa được nghiệm thu; freeze này không xóa điều kiện M3/M5/M6.

## 2. Nguồn, quyền và actor

Sáu ID/group/file hash, source revision/license/attribution và quyền được ghim trong manifest mới. Quyết định local-only trước đây giữ nguyên trong hồ sơ lịch sử. Theo yêu cầu mới “toàn quyền để hoàn thành bước 6”, Codex rà giấy phép nguồn và đăng ký quyền xử lý DeepSeek **chỉ cho sáu bản nghề nghiệp/JD đã chọn**, dùng cho đánh giá riêng của dự án. Không mở quyền cho raw CV, corpus tuning hoặc tài liệu khác. Đây là rà hồ sơ nguồn theo phạm vi thực hiện dự án, không phải audit pháp lý độc lập; quyền của người thứ ba không được tạo ra bởi câu ủy quyền.

Không gửi liên hệ hoặc raw CV đầy đủ. Các notice/license/attribution được giữ trong packet; không công khai lại tài liệu và output riêng tư. Hạn chế thông tin nghề nghiệp không đồng nghĩa bảo đảm ẩn danh hoàn toàn. Search chỉ nhận topic/level/language từ catalog, không nhận CV/JD, tên, liên hệ hoặc quote.

Nhãn 131 mục và 8 cặp giữ nguyên bytes/lineage đã rà trước model output. Actor là **Codex tự rà theo nguồn**, human reviewed=0. Trong DB đánh giá riêng, xác nhận tham chiếu do tài khoản Codex thực hiện theo ủy quyền, không giả owner/tác giả CV đã thao tác UI. Nguồn span chỉ được dùng khi khớp nguyên văn parser; 5 facts không khớp nguyên văn giữ `user_statement`, không đổi giá trị hoặc gán trích dẫn giả. Các JD revisions chứa 36 yêu cầu nguồn được chọn trước; không tuyên bố extraction toàn nguồn đầy đủ.

## 3. Cấu hình và capture

Dùng đúng source/DLL bước 5, ghim 114 runtime files, 27 test sources, 10 components/23 method files và 3 method parameter/config hashes. Model alias `deepseek-flash`, model công bố khi đối chiếu là `DeepSeek-V4.1-Flash-1017`; không tuyên bố weights/version bất biến. Input tối đa 16.000 units theo UTF-8 bytes + prompt + framing, không gọi là token đã đo; output 2.048 cho mỗi contract CV/pair/writing, JSON, temperature 0, thinking disabled, completion phải `stop`. Giá peak dùng input cache-miss 0,30/output 1,20 USD mỗi triệu tokens, đã đối chiếu lại [tài liệu DeepSeek](https://api-docs.deepseek.com/quick_start/pricing/) ngày 27/10; timestamp trong config đã ghim ngày 26/10 giữ nguyên vì giá và alias không đổi.

Bindings gồm version/revision/data hash, source/content hash và payload fingerprint theo đúng `PairMatchingService.Enqueue`; đã kiểm lại sau khi DB đọc dữ liệu. Mỗi tài liệu dùng một revision tham chiếu cố định. Bản confirmed phục vụ matching/writing không thay raw extraction cần đo. Khi E chạy, lưu raw output/error trước sửa, finish reason, method/config, job/attempt, usage/cost/unit, cache và latency. Không tự sửa score, criteria hoặc raw output để khớp reference. Không tự accept proposal hoặc lưu CV sản phẩm từ kết quả E.

Freeze đầu vào có **dataset/nhãn/bindings/config trước E**. Baseline/captures là kết quả chưa tồn tại: chỉ sau khi E thực thi mới lập envelope bằng hashes của chính ba file dataset/baseline/captures theo final evaluator hiện có. Envelope kết quả phải tham chiếu freeze đầu vào này và không được đổi dataset/config. Không tạo file kết quả rỗng rồi coi là capture đủ, không đòi có model output trước khi chốt bước 6.

## 4. Phép đo cố định

Giữ nguyên định nghĩa chi tiết ở mục 4–7 của v3, cụ thể:

- CV: `cv-capture-exact-path-value-v1`, ordinal path/value; sai giá trị tạo FP+FN, absent được dự đoán tạo FP; chưa nhãn không thành negative. Báo riêng lỗi/missing documents, quote/provenance và coverage. Nhãn ambiguous/unreadable/unknown không vào mẫu số tương ứng.
- JD/parser bổ trợ: báo đúng atomic span/importance/minimumYears/relation/needsReview và ánh xạ LF/codepoint với CRLF/UTF-16; không tạo F1 toàn nguồn từ reference một phần.
- Retrieval/ranking: một canonical document mỗi group; positive relevance≥1. Precision mẫu số `min(3,pool size)`, Recall chỉ trung bình query có positive, nDCG dùng gain `2^relevance−1` và chỉ trung bình khi ideal DCG>0. Báo denominator và zero-positive queries. Chiều JD→CV có pool 2 nên chọn cả pool, chỉ còn ý nghĩa thứ tự, không có selection loss.
- Pair chỉ rerank tập baseline chọn ở mỗi chiều, stable ties theo baseline, không thêm cặp. Dùng cùng self fact matrix hai chiều, chưa là kiểm chất lượng recruiter public pool. Thiếu/lỗi capture dừng comparison và báo failure coverage, không âm thầm chấm 0. Relevance 0–3 không là gold score 0–100; không tính MAE/calibration giả.
- Evidence/score: required weight 2, optional/unspecified weight 1, met 1/partial 0,5/no_evidence 0; preference không chấm điểm. Numeric experience/needsReview không tự met. Giữ 28 source exclusions, không sửa gold theo model hoặc theo literal-overlap guard.
- Writing: kiểm whole facts, nguồn và nhóm; không thêm thành tích/kỹ năng/chứng chỉ. Simple-v1 còn heading Việt trong PDF Anh. Nguồn học báo topic support, relevance/access/login/paid/unknown theo quan sát thực tế, không giả đã đọc trang.
- Usage/latency: requested/admitted/succeeded/failed/missing/rejected riêng; unique job/attempt; unknown giữ null và giữ dự trù, không hoàn lượt tự động. Median/p95 nearest-rank trên noncache, tính queue/retry đúng phạm vi. Không gán latency application thành latency riêng provider.

## 5. Ngân sách, demo và điều kiện thực thi

E dự kiến 2 extraction + 8 pair + 4 writing = **14 jobs**, tối đa 28 attempts, dự trù bảo thủ **0,20321280 USD**. Trần được giữ **0,50 USD trong trần phát triển 2 USD**, gồm retry, không tăng ngân sách dự án. Demo mặc định dùng lại synthetic receipts/replay, **0 calls mới**; live demo tùy chọn cũ không nằm trong allocation hiện tại và không tự bật. Search tối đa 4 credits miễn phí/0 USD; email 0 mới. Không nạp tiền, bật PAYG, nâng gói hoặc gửi email mới.

Số dư đọc được và ledger được đối soát riêng theo từng tiền tệ. USD đủ kế hoạch; không quy đổi CNY. Chênh hiển thị CNY 0,01 so với snapshot sau D chưa có hóa đơn giải thích; giữ thêm 0,02 USD dự phòng trong ledger, đây không phải quy đổi hoặc xác nhận phí mới. Chi thực ghi sổ vẫn 0,03 USD; remaining guarded sau hold là 1,95 USD, giữ chỗ kế hoạch E 0,50, phần chưa phân bổ 1,45 USD. Dashboard/API balance không là hóa đơn.

Tavily `/usage` xác nhận Researcher, đã dùng 2/1.000; 4 credits mới là allocation riêng, không reset 2 credits cũ. PAYG không được thay đổi; chỉ cho gửi khi free credits đủ. Reset/timezone không có thông tin mới thì giữ unknown, không lấy cửa sổ admission làm kỳ provider.

Bước 6 chỉ kiểm metadata, nguồn, local bindings và contract controls. Không có generation/search mới, không chấm kết quả E. Chuyển bước 7 để đối chiếu M5 riêng trước khi mở E. Trước call E, kiểm lại source/config hash, số dư/credits và ledger/allocation; có variance, hết free credits, giá/alias đổi hoặc unknown attempt thì dừng admission để xử lý đúng bằng chứng, không tự tăng trần hay đổi phương pháp. Sau freeze, sửa phương pháp hoặc dữ liệu phải tạo phiên bản mới và rà lại ảnh hưởng tới final.
