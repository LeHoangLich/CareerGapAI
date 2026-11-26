# Cẩm nang thuyết trình và phản biện CareerGap AI

Cập nhật **31/10/2026** trong thư mục bàn giao30/10. Đây là ghi chú mới, không phải build/demo/PPTX mới. Biên tập: Codex (Authorized by Project Owner); chủ dự án ủy quyền: Lê Hoàng Lịch. **M6 chờ đánh giá ngoài, đang hoãn.** Câu hỏi dưới đây là chuẩn bị phản biện, không phải nhận xét đã nhận từ giảng viên.

## Phần 1 — Bài toán, kiến trúc và quyết định chấp nhận pilot

### Lời mở đầu gợi ý

“CareerGap AI hỗ trợ ứng viên quản lý CV, tìm việc và đối chiếu CV–JD có bằng chứng; hỗ trợ nhà tuyển dụng quản lý đơn đến sàng lọc. AI đưa gợi ý, không tự tuyển hay loại. Đồ án do một người thực hiện, mục tiêu ba tháng, ngân sách định hướng khoảng4 USD/tháng. Tôi tập trung vào luồng có thể kiểm chứng, quyền truy cập và bảo toàn dữ kiện.”

Kiến trúc: React/TypeScript ở frontend; ASP.NET Core API; PostgreSQL với dữ liệu quan hệ và payload JSONB có phiên bản; worker xử lý AI, hợp đồng đầu vào/đầu ra và kiểm nguồn. DeepSeek/search/email đi qua adapter và hạn mức. Demo Fake/Synthetic tách khỏi bằng chứng provider thật lịch sử; không gọi provider khi bảo vệ.

### Công bố số liệu trước khi giải thích

Owner đã chấp nhận `pilot_2cv_4jd_limited` ngày30/10 để trình bày/bảo vệ với giới hạn công khai, không phải chứng nhận production hay nghiệm thu hội đồng. Bộ gồm **2 CV Anh × 4 JD, 6 nhóm, 131 nhãn final = 95 CV + 36 JD, 8 cặp, k=3**.

| Chỉ số | Giá trị lưu trong bằng chứng | Cách nói ngắn |
|---|---|---|
| Exact precision CV | 0.7962962962962963 | khoảng0,796 |
| Exact recall CV | 0.5119047619047619 | khoảng0,512 |
| Exact F1 CV | 0.6231884057971014 | khoảng0,623 |
| Trạng thái matching khớp | 34/44 | 34 trên44 tiêu chí đủ điều kiện |
| Baseline CV→JD nDCG@3 | 1.0 | 1,000 |
| Rerank CV→JD nDCG@3 | 0.9169956161990744 | khoảng0,917 — giảm so baseline |
| Partial precision/recall/F1 | null / null / null | chưa định nghĩa và đo |

Số nói ngắn là làm tròn để trình bày; bảng giữ số đầy đủ. F1 trên **95 nhãn CV**, không trên toàn131CV/JD: TP43/FP11/FN41, 84present+11absent. Sau sửa CV2/2, pair8/8 thành công không có nghĩa chính xác100%. Writing E4/4 qua kiểm dữ kiện/nhóm bởi Codex, không là đánh giá văn phong độc lập. Giữ lịch sử E đầu CV0/2, pair2/8; không xóa lỗi để làm đẹp kết quả.

### Vì sao owner chấp nhận?

“Tôi chấp nhận phạm vi pilot để báo cáo kết quả có thể truy vết, gồm cả kết quả âm. Baseline đạt trần1.0 trên mẫu này, rerank giảm nên tôi không tuyên bố cải thiện. Đây là quyết định phạm vi của chủ dự án, không đặt ngưỡng đạt hồi tố hoặc thay đánh giá hội đồng.”

Giới hạn phải công bố: 0CVViệt; 2JDAnhCNTT và2JDViệtngoàiCNTT nên ngôn ngữ/ngành bị trộn; CV là PDF dẫn xuất phần nghề nghiệp; sau sửa là regression trên tập đã xem, không holdout mới; human direct review=0; chưa có phản hồi ngoài. PoolJD→CV chỉ2CV với k3 không chứng minh khả năng lọc quy mô lớn. Không gọi chất lượng đại diện toàn ngành, đa ngôn ngữ đã được chứng minh hoặc production-ready.

Bằng chứng: [chấp nhận pilot](../../report/quality-acceptance-2026-10-30.md), [manifest chất lượng](../../evaluation/quality-acceptance-2026-10-30.json), [metric E](../../evaluation/stage-e-followup-2026-10-27.json).

## Phần 2 — Liêm chính học thuật và rà318 nhãn

“Chủ dự án ủy quyền; Codex trực tiếp đối chiếu kỹ thuật. Không có bằng chứng người thật đã trực tiếp rà từng nhãn; human direct review vẫn0.”

| Bộ | Tài liệu | Nhóm | Nhãn |
|---|---:|---:|---:|
| Final | 6 | 6 | 131 |
| Tuning | 7 | 4 | 164 |
| DOCX bổ sung | 1 | 1 | 23 |
| Tổng | 14 | 11 | 318 |

Quy trình trình bày tuần tự:

1. Ghim nguồn/text SHA-256; đọc PDF/DOCX/JD, giữ biến thể cùng nhóm.
2. Đối chiếu giá trị/trạng thái/ngữ cảnh:299present/18absent/1ambiguous. Absent nói về nguồn được chọn, không kết luận năng lực ứng viên.
3. Chuẩn hóa tọa độ **300 nhãn có trích dẫn**: quote, start/end Unicode codepoint, trang/dòng/ngữ cảnh. Đây không phải300 lỗi nội dung.
4. Thêm **3 cấu trúc needsReview**: danh sách ngành học mở jd-vi-05 requirements[1]; ví dụ ML/AWS jd-en-01 requirements[11]/[12]. Không ép ví dụ thành yêu cầu bắt buộc. Không nói toàn corpus chỉ có3 needsReview.
5. Tạo overlay mới, giữ nhãn lịch sử dùng tính metric. Ba lượt validator offline đạt; exact spans/hash/split đạt. Không suy thành độ đúng ngữ nghĩa được chuyên gia chứng nhận100%.

Script kiểm riêng hỗ trợ tái kiểm, **không tạo reviewer độc lập**. Không gọi đây là “audit độc lập” về con người. Không tính lại/nâng F1 bằng overlay sau thí nghiệm. Kiểm12 pair references ở lượt này là kiểm cấu trúc/schema/nhóm, không tái chấm relevance. Mở [biên bản nhãn](../../report/label-review-2026-10-30.md), không chiếu corpus private hoặc liên hệ cá nhân.

## Phần 3 — Kịch bản phản biện và ranh giới phạm vi

Ma trận30/10: **35 mục = 13 Cần sửa + 14 Cần giải thích + 8 Ngoài phạm vi**. Ngày31/10 A12 sửa/verify ở source; A13 đối soát xong, chưa phát hiện lỗi chức năng trong luồng rà, giới hạn nguồn Việt vẫn tồn tại. Không nói cả13mục đều là bug hoặc mọi chất lượng đã đạt.

### Q1 / B01 — Vì sao rerank không vượt baseline?

Baseline1.0 là trần nDCG trên đúng mẫu/nhãn này, không hoàn hảo trên mọi dữ liệu. Rerank chỉ đổi thứ tự tập đã chọn; 0.9169956161990744 là regression. Chưa có ablation để quy nguyên nhân cho riêng model/prompt/ties. Bài học: giữ baseline mạnh, công bố kết quả âm, kiểm phương pháp tiếp theo trên tập độc lập mới.

### Q2 / B02 — Vì sao F1 exact chỉ khoảng0,623?

Exact yêu cầu path/index/value; khác thứ tự mảng kỹ năng có thể bị tính sai dù từ xuất hiện trong nguồn. P43/54, R43/84, F1=86/138. Recall thấp hơn precision là quan sát, không chứng minh đã tối ưu trade-off. FP không đồng nghĩa tất cả là bịa;28dự đoán ngoài đường dẫn có nhãn không chấm. Parser recovered81/84 không phải partial F1. Không đổi luật chấm sau khi thấy kết quả để tăng điểm.

### Q3 / B03 — Vì sao PostgreSQL JSONB thay vì NoSQL thuần?

Tài khoản/owner/quyền/trạng thái/FK cần ràng buộc quan hệ; JSONB lưu payload có schema version. Ghim revision/method giúp truy vết. JSONB không tự đảm bảo schema nghiệp vụ hay bất biến; cần validation/ràng buộc backend và database. Query/index/đổi payload phức tạp hơn là chi phí cần cân nhắc, không khẳng định PostgreSQL luôn tốt hơn mọi NoSQL.

### Q4 / A13 — Tìm tiếng Việt sao có video Anh?

Adapter dùng Docker+beginner+tiếng Việt trong query, không có bước bảo đảm ngôn ngữ nội dung. Ba kết quả có tiêu đề/mô tả Anh, phụ đề tự động en; UI ghi “ngôn ngữ truy vấn” và “Chưa xác minh ngôn ngữ toàn trang”. Cả3 phù hợp Docker nhập môn theo đề cương. HTTP200/playerOK ở lượt đọc ẩn danh không thấy gate trả phí/login; chưa trực tiếp phát toàn bộ video, không suy ra giấy phép Open Access. Đây là giới hạn nguồn, chưa phát hiện bug adapter trong phạm vi rà; không sửa logic để che hạn chế. Xem [đối soát5 tiêu chí](../../report/stage-e-learning-source-review-2026-10-31.md).

### Các câu hỏi B04–B14

| ID / câu hỏi | Luận điểm trả lời |
|---|---|
| B04 — Vì sao snapshot? | Đánh giá đúngCV/JD lúc nộp dù nguồn hiện tại đổi. File version khác structured revision; sửa tạo bản mới. Quyền hiện tại/retention vẫn áp dụng, không lưu mãi chỉ vì bất biến. |
| B05 — Vì sao k3? | Quyết định phạm vi/chi phí, chưa chứng minh tối ưu. Pool2CV giới hạn chiều ngược; query idealDCG0 không vào mẫu số nDCG tương ứng. |
| B06 — Vì sao simple-v1? | Owner chốt một cột để tập trung dữ kiện/dấu/PDFtext/version; chưa chứng minh tương thích mọiATS. |
| B07 — U1–U5 có đủ nghiệm thu? | Quan sát owner, không người đánh giá độc lập; không có điểm/thời gian thì không tự điền. FakeUI không chứng minh chất lượng model thật. |
| B08 — Owner chấp nhận sao M6 chờ? | Scope pilot đã chốt; đánh giá ngoài là bằng chứng khác, đang hoãn. Không hỏi chốt lại scope hoặc tự đóngM6. |
| B09 — Fake/AIoff thay provider thật? | Không. Fake kiểm luồng/quyền; AIoff 503 là tắt cấu hình local, không mọi timeout/429/500 provider. |
| B10 — Pha ngôn ngữ/token chung? | Chọn ngôn ngữ trình bày không đảm bảo dịch mọi dữ kiện. Thêm stopwords chưa được ablation chứng minh; không tune theo final đã xem. |
| B11 — Closed còn xemURL? | Closed dừng nhận đơn, archived ẩn nội dung. Direct200/acceptingApplications=false không tự thành lỗi quyền; không nói đã retest ca này trong đợt biên tập. |
| B12 — Điểm0–100 quyết định tuyển? | Rubric thử nghiệm, không xác suất tuyển. no_evidence chỉ thiếu dữ kiện, user_statement khác span. Recruiter quyết định trạng thái; writing phải được duyệt. |
| B13 — Budget/unknown? | Snapshot lịch sử spent0.11+hold0.03, remaining1.86/cap2USD, không balance mới/hóa đơn. Unknown giữ hold. Tavily4credits=D2+E2/allowance0; email1accepted/1unknown. |
| B14 — Tái lập/an toàn? | Release30 có cleanbuild, restore43tables/7objects/DPkeys, rollback local synthetic. Không suy thành SLA/cloud/OSbất kỳ hoặc phần trăm chất lượngAI. |

### Ngoài phạm vi C01–C08

| ID | Không cam kết hiện tại | Hướng sau đồ án |
|---|---|---|
| C01 | Cào hàng chục ngànJD | Nguồn có quyền, dedup/lineage, pilot tăng tải |
| C02 | Pre-train/fine-tuneLLM riêng | Quyền/nhãn/holdout/baseline, đo chi phí; không nói mọi fine-tune bất khả thi |
| C03 | ATS enterprise đa tenant/phỏng vấn/offer/HR | Khảo sát workflow/cách ly tenant; hiện đến sàng lọc |
| C04 | OCR mọi scan/layout | Pipeline riêng với benchmark ảnh hợp lệ |
| C05 | Nhiều mẫu/dịch hoàn toàn tự động | Thêm từng mẫu, proposal dịch có nguồn và user duyệt |
| C06 | Cloudproduction/HA/SLA/emaildomain | Kế hoạch TLS/storage/quota/monitoring và nghiệm thu riêng |
| C07 | Mobile/chat/billing/LMS | Ưu tiên sau dùng thử; gợi ý link không phảiLMS |
| C08 | AI tự tuyển/loại/tự sửaCV | Không định triển khai trong thiết kế hiện tại; giữ người dùng quyết định |

Nguồn: [ma trận35 mục](../../report/feedback-triage-2026-10-30.md), [tái kiểmA12/A13](../../report/a12-a13-verification-2026-10-31.md). Không có bằng chứng thì nói “chưa đo/chưa xác minh”.

## Phần 4 — Live demo an toàn

### Chuẩn bị và phiên bản

Theo [hướng dẫn chạy](huong-dan-dung-thu.md), dùng tài khoản private trên máy; không chiếu mật khẩu. Demo5190 release20261030 là Fake/Synthetic, launcher không nạp.env. Không mở provider dashboard, gọi search/email thật hoặc sửa idempotency/DB để ép kết quả.

**A12 chưa có trong runtime 5190/source ZIP30/10.** Source mới: backend160/unit15/E2E22; release30 cũ: backend160/unit11/E2E21. Không gán test mới cho binary cũ. Với runtime30, trình bàyA12 bằng biên bản/test; muốn liveA12 cần runtimeFake mới được đóng gói/kiểm riêng. Đợt biên tập này không tạo runtime đó. PPTX/video nền29/10 giữ ngày riêng, không chứng minhA12.

### Luồng 1 — Candidate

1. Đăng nhập Candidate → Khám phá việc làm → chọn đúngCV/version đã phân tích. Nói rõ synthetic/Fake.
2. Tìm theoCV; phân biệt retrieval score với rubric0–100.
3. Bấm **Xem kết quả đã lưu**, mở tiêu chí/nguồn. GET không tạo job/attempt/phí, trả dữ liệu lần trước trong ngữ cảnh/quyền hiện tại.
4. Reload rồi mở lại cùng cặp. Chưa có kết quả thì báo đúng, không nói nút tự phân tích mới.
5. Tiêu chíDocker → **Tìm nguồn học cho tiêu chí này** → **Tìm tài liệu học**. Nhãn mô phỏng hiện rõ. Đây không phải3videoE; dùng bảng đối soát đã lưu để nói về lượtE thật.

### Luồng 2 — Quyền CV và Recruiter

1. Nếu demo ẩn liên hệ, dùng **Candidate khác** xemCVcông khai để kiểm bản dẫn xuất ẩn liên hệ. Không tự công khai CV riêng ngoài lựa chọn của chủCV.
2. Đăng nhập Recruiter owner tin → mở đơn và CVsnapshot. Theo đặc tả, recruiter được xem bản gốc/snapshot có liên hệ; không nói recruiter luôn nhận bản ẩn liên hệ.
3. Đối chiếu CVversion/JDsnapshot lúc nộp với bản hiện tại. SửaCV/JD sau nộp không thay hồ sơ đã nộp; không sửa snapshot để làm bản ẩn liên hệ.
4. Chuyển trạng thái hợp lệ, ví dụ submitted→reviewing, ghi chú. Nếu đơn đãreviewing, chọn dữ liệu thích hợp, không ép chuyển lặp. Quyết định là của recruiter, khôngAI.

### Luồng 3 — AI tắt và reset

1. Chạy `python scripts/handoff-demo.py fault --release 20261030`. Yêu cầuAI mới nhận503/ai_disabled, thông báo vẫn xem/tảiCV/thao tác thủ công.
2. Mở lại kết quả cũ và tảiCV; không gọi đây là kiểm mọi lỗi mạng. Khôi phục bằng `python scripts/handoff-demo.py start --release 20261030`.
3. Giải thíchA12 ở source đã kiểm: invalid_or_expired_reset→lỗi link/nút yêu cầu mới; password_policy→12–256ký tự; 500/503/network→lỗi hệ thống. Không suy nguyên nhân used hay expired chỉ từ cùng mã.
4. Kịch bản cho runtime mới sau khi đồng bộ: tài khoản synthetic→forgot→hộp thưFake→reset thành công→dùng lại link→**Yêu cầu gửi lại liên kết mới**→forgot→gửi yêu cầu mới. Nút chuyển form xóa query token, không tự gửi email. Không chiếu token/password.
5. Backend đã kiểm token hết hạn bằng chờ thời gian; E2E kiểm link đã dùng và chuyển forgot/gửi mới, không E2E chờ hết hạn. Mở [biên bảnA12](../../report/password-reset-feedback-2026-10-31.md).

### Sự cố và kết thúc

Runtime không sẵn sàng thì dùng video đúng phiên bản, nói rõ đang xem bản ghi. Lỗi chưa dự kiến thì ghi nhận, không chuyển sang provider thật. Kết: “Owner đã chấp nhận pilot với giới hạn công khai; bằng chứng kỹ thuật có thể kiểm lại. Chưa có phản hồi giảng viên/người đánh giá ngoài; M6 còn chờ.”
