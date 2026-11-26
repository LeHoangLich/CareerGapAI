# Chốt phạm vi chất lượng pilot — 30/10/2026

**Chủ dự án Lê Hoàng Lịch đã trực tiếp chấp nhận phạm vi `pilot_2cv_4jd_limited` là đủ để trình bày/bảo vệ đồ án, với kết quả và giới hạn công khai dưới đây.** Đây là quyết định chấp nhận pilot của chủ dự án sau khi có kết quả, không phải ngưỡng chất lượng đăng ký trước, quyết định của giảng viên, hay bảo đảm hội đồng sẽ nghiệm thu.

- `acceptor`: `Lê Hoàng Lịch (Project Owner)`.
- `technical_compiler`: `Codex (Authorized by Owner)`.
- `scope`: `pilot_2cv_4jd_limited`.
- Căn cứ: chỉ thị trực tiếp của chủ dự án trong chat ngày 30/10/2026, phê duyệt bộ 2 CV Anh × 4 JD và kết quả Stage E, đồng thời yêu cầu giữ M6 chờ.
- Chỉ đóng mục **chốt phạm vi chất lượng pilot bởi owner**. Không thay đổi trạng thái nghiệm thu M3/M5/M6; M6 vẫn chờ đánh giá bên ngoài, đang được owner tạm hoãn.

## 1. Phạm vi và cách đọc metric

Sáu nhóm nguồn: 2 CV PDF tiếng Anh ngành CNTT và 4 JD (2 tiếng Anh CNTT, 2 tiếng Việt ngoài CNTT). Có 131 nhãn trường final = **95 nhãn CV + 36 nhãn JD**, 8 cặp CV–JD, 72 nhận định tiêu chí, 28 exclusions và k=3. Vì vậy **không tính CV precision/recall/F1 trên cả 131 nhãn**.

Giá trị thập phân dưới đây giữ nguyên số đã lưu trong manifest, không làm tròn thêm. Phân số được ghi khi cần thể hiện mẫu số chính xác; không coi biểu diễn float hữu hạn là phân số thập phân vô hạn chính xác. Metric thuộc phương pháp và artifact ngày 27/10, không phải phép đo provider mới trên DLL demo30/10.

## 2. Rubric chấp nhận pilot theo bằng chứng

| Hạng mục | Tiêu chí ghi nhận | Quan sát thực tế | Quyết định trong pilot |
| --- | --- | --- | --- |
| Đọc/trích xuất CV | Công bố thành công và lỗi; đo exact trên nhãn đã có trước sửa tay | E đầu 0/2; sau sửa 2/2; P/R/F1 ở mục 3 | Chấp nhận để báo cáo pilot, giữ hạn chế recall; không công nhận extraction đạt chuẩn đại diện |
| Matching | Có kết quả/nguồn đối chiếu, đối sánh trạng thái và baseline; không bỏ lỗi để làm đẹp metric | E đầu 2/8; sau sửa 8/8; trạng thái 34/44; CV→JD nDCG giảm | Chấp nhận cả kết quả âm; không kết luận rerank cải thiện |
| Writing | Đề xuất giữ dữ kiện và nhóm nguồn; phân biệt kiểm kỹ thuật với lựa chọn của người dùng | E 4/4 thành công và qua kiểm whole facts/groups bởi Codex | Chấp nhận bằng chứng chức năng trên mẫu hẹp; không coi là human preference/đánh giá văn phong độc lập |
| Search | Tách kết quả truy vấn, độ đúng/ngôn ngữ nguồn, owner review và credits | D owner rà 2 nhóm đạt; E 2 truy vấn/6 kết quả/4 URL, chất lượng nội dung Việt chưa được xác nhận | Chấp nhận tích hợp và giới hạn đã ghi; chưa đánh dấu chất lượng nguồn Việt đạt |
| Email | Nhận email, reset và đăng nhập trên tài khoản owner; phân biệt unknown/accepted | 2 attempts: 1 accepted, 1 unknown; owner xác nhận Inbox/reset/login | Chấp nhận kiểm chứng localhost tới owner; không suy thành gửi email production |
| Nguồn nhãn và khả năng truy vết | Actor, source/hash, split, ngoại lệ rõ ràng | Codex rà theo ủy quyền 318 nhãn; direct human review=0 | Chấp nhận nguồn tham chiếu nội bộ có giới hạn, không gọi human/expert gold |

Rubric này tổng hợp bằng chứng đã có để ghi quyết định owner; **không đặt ngưỡng mới hồi tố rồi tuyên bố thí nghiệm vượt ngưỡng**. Các mục chưa đo vẫn chưa đo và nằm trong giới hạn được owner chấp nhận.

## 3. CV: parser và extraction là hai phép kiểm khác nhau

Lượt E đầu: 0 tài liệu scored/2 thất bại; P/R/F1 đều **null**, không gán bằng 0 và không xóa lịch sử lỗi.

Lượt sau sửa, protocol `cv-capture-exact-path-value-v1`, trước chỉnh sửa người dùng:

| Chỉ số | Giá trị |
| --- | --- |
| Tài liệu scored / tổng | 2/2 |
| Nhãn CV | 95 = 84 present + 11 absent |
| TP / FP / FN | 43 / 11 / 41 |
| Precision exact | 43/54 = 0.7962962962962963 |
| Recall exact | 43/84 = 0.5119047619047619 |
| F1 exact | 86/138 = 0.6231884057971014 |
| Correct absent | 11/11; không cộng vào TP của P/R/F1 |
| Partial-match precision / recall / F1 | **null / null / null — chưa định nghĩa và đo trong protocol Stage E** |
| Parser khôi phục giá trị nguồn | CV C: 42/42; CV Veekas: 39/42; tổng 81/84 |
| Source disagreements trên các exact TP | 0 |
| Dự đoán ngoài đường dẫn có nhãn, không chấm | CV C: 28; CV Veekas: 0 |

Parser recovered values chỉ kiểm giá trị nhãn có xuất hiện trong segment parser, không phải semantic partial-match F1. Không dùng 81/84 để điền metric partial còn thiếu. Exact so sánh cả path/index và value, nên khác thứ tự mảng có thể bị wrong_value/missing. FP không đồng nghĩa mọi trường là bịa; trường chưa có nhãn không tự thành FP. Đây là đánh giá **một phần nội dung được gán nhãn**, không bao phủ toàn CV. Không bổ sung phép đo partial hồi tố trong đợt tổng hợp này.

## 4. Pair matching và ranking

E đầu: 2/8 cặp thành công, 6/8 thất bại; chỉ 10/10 trạng thái trong hai cặp thành công khớp. Rerank toàn tập **null** vì ma trận thiếu; không gán điểm 0 cho cặp lỗi.

Sau sửa: 8/8 cặp thành công, **34/44** trạng thái đủ điều kiện khớp tham chiếu; 44 = 72 nhận định trừ 28 exclusions. Evidence ở kết quả hợp lệ qua kiểm exact nguồn. Phương pháp kết hợp backend xác định với LLM: backend có thể điền `no_evidence`; không tuyên bố LLM tự đánh giá mọi tiêu chí.

| Hướng và mẫu số | Metric | Baseline | Rerank |
| --- | --- | ---: | ---: |
| CV→JD, 2 query | Mean Precision@3 | 0.6666666666666666 | 0.6666666666666666 |
| CV→JD, 2 query có positive | Mean selection recall | 1.0 | 1.0 |
| CV→JD, 2 query | Top-1 relevant rate | 1 | 1 |
| CV→JD, 2 query ideal DCG > 0 | Mean nDCG@3 | 1.0 | 0.9169956161990744 |
| JD→CV, 4 query | Mean Precision@3 | 0.5 | 0.5 |
| JD→CV, 2 query có positive | Mean selection recall | 1.0 | 1.0 |
| JD→CV, 4 query | Top-1 relevant rate | 0.5 | 0.5 |
| JD→CV, 2 query ideal DCG > 0 | Mean nDCG@3 | 1.0 | 1.0 |

Rerank chỉ sắp lại tập baseline chọn; không phải tìm kiếm lại toàn pool. Mỗi hướng có 8 judged pairs, 6 selected pairs và 4 positive selected pairs. Chiều JD→CV có 2 query không positive, 3/4 query có kết quả; precision dùng mẫu số `min(k, pool size)` theo evaluator (pool=2), slot không trả về tính 0. Hai query ideal=0 không vào mẫu số nDCG/recall có positive.

**CV→JD là regression, không cải thiện baseline.** Chiều JD→CV pool hai CV với k=3 không chứng minh lợi ích loại ứng viên. Điểm matching không là xác suất tuyển hoặc thước đo năng lực được thẩm định.

## 5. Writing, search và email

Writing E: 4/4 jobs thành công (2 CV × lựa chọn vi/en), cả 4 qua kiểm nguyên vẹn dữ kiện/nhóm nguồn bởi Codex; CV C có 12 sections mỗi lượt, CV Veekas có 6 mỗi lượt. Không tự accept thay người dùng; không có metric human preference hay chứng minh bản tiếng Việt là bản dịch đầy đủ. Followup giữ phương pháp và tái dùng evidence E này; diễn tập thêm UI→PDF→version trên synthetic/Fake không được cộng thành provider samples. Evidence D riêng: 2/2 proposal synthetic DeepSeek được owner rà dữ kiện đạt, duyệt và lưu thành hai version mới; không cộng vào mẫu final thực.

Search D: 2 Basic requests Docker vi/en, 3 links mỗi nhóm, 2 credits; owner báo cả hai nhóm đạt ngày23/10. Search E sau sửa: 2 Basic requests, 6 kết quả/4 URL duy nhất; hai URL YouTube đọc lỗi429, một chỉ đọc trang khung, một Medium đọc được. Không suy 429 là trả phí; không suy mọi nguồn đều miễn phí. Ngôn ngữ vi/en là ngôn ngữ truy vấn, không xác nhận toàn trang/video. **Chất lượng nguồn học tiếng Việt E chưa được nghiệm thu**; review D không chuyển thành review E.

Tavily Dashboard owner lúc **17:13 ngày28/10/2026, Asia/Saigon**: **4/1000 credits**, bằng D2+E2; Researcher Free, PAYG tắt, paygo credits=0. Đối soát tổng đã xong; Usage chi tiết bị khóa, reset chỉ biết monthly, chưa có ngày/giờ/múi giờ chính xác. Allowance còn0, hai credits E chưa dùng đã đóng; đợt này không cấp lại lượt.

Email D: 2 provider attempts, 1 accepted và 1 original unknown được giữ. Owner nhận Inbox, reset và đăng nhập bằng mật khẩu mới; thử lại link cũ gặp lỗi UI và vẫn đăng nhập được. UI lỗi chung không chứng minh riêng nguyên nhân token đã dùng; kiểm HTTP Fake riêng 1/1 xác minh single-use. Dashboard sau send: monthly1/3000, daily0/100 ở thời điểm ghi nhận, không phải số dư mới ngày30/10. Provider delivered event chưa xác minh độc lập; Inbox là bằng chứng owner. Sender test của Resend tới tài khoản owner, chưa verified domain dự án/production. E có 0 email mới; đợt này có 0.

## 6. Nhãn, exposure và giới hạn được công khai

1. Có 2 CV Anh, 0 CV Việt; không chứng minh chất lượng đa ngôn ngữ.
2. Có 2 JD Anh CNTT và 2 JD Việt ngoài CNTT; language-domain confounding chưa tách được, thiếu JD Việt CNTT.
3. CV đo là PDF dẫn xuất phần nghề nghiệp, không đại diện toàn bộ CV gốc, DOCX hay nhiều layout. Không bù số nhóm bằng variants/synthetic.
4. **Các số sau sửa được chấp nhận ở đây là regression trên tập đã xem, không là holdout mới.** Tuning đã được xem trong phát triển; parser/JD có exposure trước. Lượt E đầu giữ freeze theo phương pháp đăng ký và giới hạn exposure đã công bố; không viết lại lịch sử thành mọi phương pháp cùng pristine hoặc mọi final đều dùng tuning trước E đầu.
5. Rerank CV→JD giảm từ1.0 xuống0.9169956161990744, không cải thiện; pool JD→CV suy biến khi k lớn hơn số CV.
6. Human direct label review=0. Lượt30/10: Codex theo ủy quyền rà318 nhãn (131final+164tuning+23DOCX), không phải expert review độc lập.
7. Overlay30/10 bổ sung locator và 3 phân loại needsReview; không thay nhãn historical đã dùng để ra metric E. Metric trong biên bản này **không phải kết quả đo lại với overlay mới**. Partial-match và chất lượng nguồn Việt còn chưa đo/xác nhận đầy đủ.
8. Chưa có phản hồi giảng viên/người đánh giá ngoài; owner chấp nhận pilot không thay phản hồi đó. M6 vẫn chờ và việc mời đánh giá vẫn hoãn.
9. Không tuyên bố representative quality, production-ready, độ chính xác trên toàn ngành, hoặc chất lượng dịch thuật hoàn chỉnh.
10. Bản demo hiện hành Fake/synthetic phục vụ diễn tập; tách khỏi bằng chứng provider thật ngày27/10. Không gán metric lịch sử cho phiên bản triển khai chưa được đo tương ứng.

## 7. Chi phí, bảo toàn và trạng thái tiếp theo

E đầu upper bound0.03049410USD; followup upper bound0.03773130USD, debit bảo thủ0.04USD cho mỗi đợt. Ledger tổng ghi spent0.11USD + holds0.03USD, guarded remaining1.86USD trong cap2.00USD theo lần đối soát đã lưu; không phải hóa đơn mới hoặc quy đổi CNY. Giữ unknown/reservations, không reset/refund. Quyết định chấp nhận này không cấp thêm API/chi phí/quyền chuyển dữ liệu.

**Mục chốt phạm vi chất lượng pilot: hoàn thành theo quyết định owner.** P20 vẫn chờ phần đánh giá ngoài; M6 chưa nghiệm thu. Không nâng trạng thái M3/M5. Không yêu cầu owner xác nhận lại quyết định vừa cấp. Nhãn, metric, ledger và bộ bàn giao release30 giữ nguyên. Chỉ thêm biên bản/manifest và ghi trạng thái hiện hành; không gọi provider, không chạy lại thí nghiệm, không sửa code.

## Nguồn bằng chứng

- [Kết quả E đầu](../evaluation/stage-e-results-2026-10-27.json) và [báo cáo](stage-e-2026-10-27.md).
- [Kết quả sau sửa](../evaluation/stage-e-followup-2026-10-27.json) và [báo cáo](stage-e-followup-2026-10-27.md).
- [Bổ sung owner/Tavily28/10](stage-e-external-evidence-2026-10-28.md).
- [Rà nhãn30/10](label-review-2026-10-30.md).
- [Search D](../evaluation/stage-d-search-provider-2026-10-23.json), [writing D](../evaluation/stage-d-writing-provider-2026-10-23.json), [email D](../evaluation/stage-d-email-provider-2026-10-24.json).
- [Manifest chốt chất lượng](../evaluation/quality-acceptance-2026-10-30.json) ghim SHA-256 từng bằng chứng; báo cáo CV và writing chi tiết giữ private, không đưa dữ liệu cá nhân vào đây.
