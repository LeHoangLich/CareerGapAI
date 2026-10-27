# Kiểm tra toàn bộ bước 2 và thực hiện bước 3 trên pilot local

Ngày đối chiếu: 25/10/2026, Asia/Saigon. Yêu cầu: kiểm lại bước2, đủ thì chuyển3hoặc nghiệm thuM5nếu đã đủ điều kiện. Kết luận audit: **bước2hoàn thành cho phạm vi pilotlocal đã được owner xác nhận; chuyển3hợp lệ, M5chưa đủ điều kiện nghiệm thu**.

## 1. Kết quả audit bước 2

Nguồn điều kiện: [checklist D→E mục2](../tasks/stage-d-to-e-checklist-2026-10-25.md), [phạm vi owner xác nhận](../evaluation/stage-d-local-pilot-scope-2026-10-25.md).

| Điều kiện | Kết quả |
|---|---|
| Danh sách và phạm vi đã chốt |Đạt: bộ12tài liệu/9nhóm, chỉ local evaluation|
| Quyền phù hợp phương pháp được phép dùng |Đạt cho local;12/12transferfalse, không dùng DeepSeek|
| Hash nguồn và bằng chứng |12/12source hashes,55/55frozenentries trên51paths khớp; receipt owner khớp corpus|
| Coverage và khoảng thiếu được ghi rõ |CVAnh4file/3nguồn; JD8file/6nhóm; nativeCVViệt0, nhãnpartial, humanpending, pairlabels ban đầu0|
| Nhóm/split |Tuning7docs/4groups/164labels; final5docs được giữ riêng, không group chéo split|
| Lịch sử exposure |Đã ghi nhận JDfinal output đã xem21/10 trong phạm vi method lịch sử; không gọi finalcũ pristine cho mọi phương pháp hiện hành|

Nhận định trước giữ bước2mở đã gộp các gate chất lượng/finalfreeze vào intake quá sớm. Điều kiện nguyên văn là phạm vi thực thi/quyền phù hợp/coverage và phần thiếu rõ; owner đã chốt pilot12local. **Sửa phân bổ công việc minh bạch, không hạ điều kiện M5 hoặc mở rộng quyền.** Nhãn cặp và rà nguồn thuộc bước3; final độc lập/freeze thuộc bước6. Các thiếu hụt nativeCVViệt, quyền đánh giá DeepSeek trên dữ liệu thực và coverage đại diện tiếp tục được giữ trong hồ sơ M5.

Biên nhận audit private: `.local/stage-d-20261023/step2/step2-full-audit-and-local-exit.json`. Không sửa corpus, nhãn, consent hoặc candidate lịch sử.

## 2. Phạm vi bước 3

Chỉ dùng7tài liệu tuning của bộ12:3fileCV/2nhóm và4JD/2nhóm, tổng164nhãn. Không gộp23nhãn DOCX ngoài bộ12; không cộng bản dẫn xuất thành người mới. Không mở source text/output model của5tài liệu final để chỉnh nhãn hoặc phương pháp.

Rà source trực tiếp, theo field/value/nhóm/quote/trang/offset và ngữ cảnh. BaCV được đối chiếu text và render các phần nghề nghiệp liên quan; header liên hệ bản gốc không được dùng trong bằng chứng cặp. Review do Codex, có agent kiểm chéo; humanreview vẫn pending, không expert/human gold.

| Kết quả nhãn trường | Số lượng |
|---|---:|
| Giữ nguyên |161|
| Sửa source context/cấu trúc |2|
| Giữ mơ hồ do nguồn |1|
| Tổng đã rà |164|

Hai sửa ởJD: một mệnh đề optional gộp kinh nghiệm vận hành AND hiểu quy trình được đánh dấu needsReview, không coi là một atomicgold đã chốt; một fragment về làm việc theo nhóm được mở quote sang mệnh đề đầy đủ và đánh dấu sharedpredicate chưa được giải quyết. Giá trị và status không tự đổi thành dữ kiện mới. Câu nguồn năm kinh nghiệm mâu thuẫn giữ minimumYearsnull/ambiguous, không ép1hoặc0.

Có7mục JD needsReview được loại khỏi mẫu số exactatomic/structured cho đến khi chốt biểu diễn;1mục source ambiguous loại khỏi exactfield/value tương ứng. Trường chưa có nhãn giữ unknown. Sáu nhãn vắng do redaction không được gọi là vắng tự nhiên; một nhãn vắng khác chỉ áp dụng phần CV đã giữ lại. Chưa tính precision/recall/F1 trong lượt rà.

## 3. Nhãn cặp và giới hạn

Chuẩn bị4cặp canonical từ2nhómCV×2nhómJD, dùng một file đại diện mỗi nhóm. Nhãn relevance thử nghiệm0–3 mô tả quan hệ tài liệu theo [protocol nguồn](../evaluation/stage-a-corpus-protocol.md), không phải điểm0–100, năng lực thực hoặc quyết định sàng lọc. Các bằng chứng/no_evidence/partial/unclear gắn đúng nguồn; missingevidence không là thiếu năng lực.

Rà chéo giữ importance theo nguồn, không nâng “yêu cầu” chung thành bắt buộc; phân biệt skilllisting với kinh nghiệm sử dụng, ngành học với trình độ và React với Nest.js. Sức khỏe không được đánh giá ở cấp ứng viên, không tham gia relevance hoặc mẫu số bằng chứng. On-site availability không suy từ địa chỉ CV. Typos/thuật ngữ gần nhau không tự thành alias đã chốt.

Đây là4nhãn cặp Codex tự rà phục vụ pilotlocal, không phải nhãn chuyên gia hoặc đánh giá đại diện. Chưa phủ các mức relevance trung gian1/2 hoặc CVnativeViệt; không suy chất lượng ranking từ một pool nhỏ như vậy. Không đổi trạng thái ứng tuyển hoặc runtime score.

## 4. Kết quả kiểm tra và trạng thái

Artifacts mới ở `.local/stage-d-20261023/step3/`: per-source reviewCV/JD, structured exclusions, pairreference, `field-labels-reviewed-v1.jsonl`, `corpus-tuning-reviewed-v1.json` và `step3-summary.json` có lineage. Corpus/nhãn/source/quyền/split gốc giữ nguyên; providertransferfalse cho tất cả tài liệu.

Validator local đạt **7docs/4groups/164fieldlabels/4pairlabels/0errors**; provider gate từ chối đúng7tài liệu. Kiểm anchors157mục(156present+1ambiguous-source),7absent được review riêng theo ngữ cảnh; hash manifest/labels khớp. Bộ cặp có58đối chiếu:8evidence/22partial/25no_evidence/1unclear/2not_assessed. Hai mục sức khỏe bị loại khỏi relevance và mẫu số bằng chứng; hai compositeReact/Nest cần review không được coi là exactstructuregold đã giải quyết. Chưa tính metric. Kết quả/summary giữ private cùng artifacts. Không gọi DeepSeek/Tavily/Resend, không chi phí, không migration/runtime/dependency, không chạy lại fullbackend/E2E hoặc CI trong lượt dữ liệu này. Các kết quả tests D lịch sử không được gán thành lần chạy mới.

**Bước3đã hoàn thành trong phạm vi Codex source review/nhãn tham chiếu của pilotlocal**; không nâng thành nghiệm thu chất lượng đại diện hoặc nhãn người thật. Bước tiếp theo là chuẩn bị pilot tuning ởbước4, không tự dispatchprovider khi chưa có quyền/allowance tương ứng. **M5chưa đạt**: cần phạm vi đánh giá hợp lệ cho các phần còn thiếu, pilot chất lượng cần thiết, final độc lập/methodfreeze/giao thức/dự toán và biên bản cùng phiên bản. Không chuyển E hoặc deploy/commit/push.
