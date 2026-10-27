# Bước 6 — Ghim phương pháp và chuẩn bị bộ đánh giá cuối

Ngày đối chiếu: 26/10/2026, Asia/Saigon. **Đã hoàn tất phần chuẩn bị local có thể thực hiện bằng đầu vào hiện có; bước 6 tổng thể chưa hoàn thành.** Chưa có bộ dữ liệu/nhãn/quyền final thực thi được cho toàn bộ đánh giá E. Chưa nghiệm thu M5, chưa chuyển bước 7 hoặc mở E.

**Quyết định owner mới nhất:**4CV×4JD là mục tiêu có điều kiện, không phải mức tối thiểu. Pool chốt theo nhóm độc lập thực có đủ nhãn/quyền; ưu tiên CNTT, coverage Việt/Anh báo riêng, giữ k=3 và0,50USD là trần dự toán, chưa cấp phép chi/call/transfer. Đã lưu hai receipt và snapshot lịch sử; [protocol v2](../evaluation/stage-d-final-protocol-2026-10-26.md) đã cập nhật. Không giữ bước6 mở chỉ vì chưa đủ4×4; các phụ thuộc quyền/độc lập/pool/references thực vẫn phải đủ.

## 1. Kiểm lại toàn bộ bước 5

Bốn điều kiện bước 5 đều đạt trong phạm vi đã rà. Kiểm lại 42 artifact bước 5, 52 artifact bước 4, bản chụp guide/report lịch sử, 114 file runtime và 27 file test backend; các hash khớp. Hai DLL và TRX bước 5 giữ nguyên, TRX có 28/28 đạt gồm ba regression mới. Không chạy lại backend/DB hoặc gán full133/E2E9/provider lịch sử cho DLL mới. [Biên bản bước 5](stage-d-step4-step5-2026-10-26.md) được giữ nguyên; guide hiện hành có snapshot trước cập nhật.

Source bundle bước 5: `17c07f2ee2c72da8795cd2420930774c8e2d683bdedf6feb03602fef31cfd383`. API DLL riêng: `a21e45f59c8ff7982660cd233a3830a2d9064cbf1b0e5d02a32703a42aa29319`. Đây là bản nguồn chuẩn bị đánh giá, working tree còn dirty; không nhận là commit/CI mới.

## 2. Đối chiếu từng phần của bước 6

| Công việc | Đã thực hiện | Phần còn chờ để chốt |
|---|---|---|
| Ghim triển khai và cấu hình | Inventory 10 nhóm component/23 file; parser/schema/prompt/validator/baseline/rubric/weights/projection/writing/learning/template; 3 cấu hình/hash AI kế hoạch | Áp dụng đúng cấu hình khi chạy; alias provider không là model weights bất biến |
| Ghim dữ liệu/nhãn/quyền/demo | Inventory metadata bộ 12/9 nhóm, split/hash/quyền; giữ 55 frozen hash entries; demo dùng evidence synthetic riêng | Nguồn final phù hợp, references/pair matrix, quyền gửi provider và phạm vi coverage chưa đủ |
| Audit độc lập final | Lập lịch sử theo từng phương pháp; giữ output parser/JD đã xem ngày 21/10 trong scope lịch sử | Nhóm final độc lập mới khi cần cho claim hiện hành; không dùng nhóm tuning thay thế |
| Giao thức và phép đo | [Protocol v2](../evaluation/stage-d-final-protocol-2026-10-26.md), owner chốt mục tiêu có điều kiện/k3/coverage; raw-before-edit, hai chiều, score/evidence, writing/learning, usage/latency và exclusions | Pool thực/references/bindings phải ghim trước output tương ứng |
| Dự toán E/demo | [Dự toán có giới hạn](../evaluation/stage-d-evaluation-budget-2026-10-26.json), tách USD/search/email và retry | Đối soát tiền trước call; 4 search credits mới chỉ là đề xuất, allowance hiện 0 |
| Bằng chứng provider | Manifest mới tham chiếu DeepSeek writing, Tavily và Resend mới nhất; không sửa candidate trước probe | Không cần chạy lại các workflow đã xong; bằng chứng cũ không tự trở thành kết quả final mới |

[Manifest chuẩn bị](../evaluation/stage-d-step6-preparation-2026-10-26.json) ghim cấu hình, protocol, kế hoạch dữ liệu/chi phí, ba manifest provider và bốn file công cụ/test. Trạng thái là `prepared_not_final_frozen`, tách với final freeze thật. Một manifest có hash đúng không tự chứng minh chất lượng, quyền dữ liệu hoặc sự đồng ý của owner.

Ba config hash được tái tạo bằng serializer/công thức của DLL bước 5, không tạo AiMethod trong DB: CV `dbdad23dc6f7408c1b778d4dc0c39c111902ab73b58cdd607293ae2a1a2bf30f`; pair `896100b6426ee62b05ad85300fbfc5f053e8009b3a1ba7e14a1c986efdcfc3f5`; writing `e483caadf4dd85ccf77210e8a23a98d4f504a55030e0f551fcea30429fbdc337`. Tham số kế hoạch input16.000/output2.048, JSON/temperature0/thinking-off/stop; thay giá/timestamp/alias/tham số phải tạo hash và bộ freeze mới phù hợp, không chỉnh để khớp kết quả final.

## 3. Tập cuối hiện có và khoảng thiếu

Bộ owner xác nhận vẫn12tài liệu/9nhóm,7tuning/164fieldlabels và5final/82fieldlabels đăng ký. Final candidate có một CVAnh PDF dẫn xuất,4JDtext (2Việt ngoài CNTT/2Anh CNTT), chưa CVgốc Việt hoặc DOCXgốc trong tập này. Nhãn cặp gốc0; đã bổ sung **4 reference cặp local** riêng, chưa final freeze. Cả12giữ `provider_transfer_allowed=false`. Bộ followup/DOCX khác vẫn tách riêng, không bù số nhóm hoặc chuyển tuning sang final.

Parser CV và JD final từng có output được xem ngày 21/10. Kết quả đó giữ giá trị lịch sử đúng phương pháp/thời điểm cũ; không đổi thành holdout mới cho protocol hiện hành. Baseline/pair/writing/learning chưa có output final được ghi nhận, nhưng chưa đủ references/phạm vi/quyền để gọi là tập cuối sẵn sàng. Audit không kết luận chung rằng mọi phương pháp đều đã bị tune trên final hoặc mọi phương pháp đều pristine.

Chính sách owner cho phép pool nhỏ hơn mục tiêu4×4 khi đủ nhãn/quyền/độc lập và nêu giới hạn. Một CV làm chiều JD→CV suy biến; không diễn giải JDViệt ngoài CNTT thành kiểm CNTT tiếng Việt hoặc kết luận hiệu ứng ngôn ngữ không nhiễu ngành. Nhận thiếu coverage đúng phạm vi, không phát sinh hard minimum mới. Không có model output final cũ/mới được mở để sửa phương pháp trong bước6 này.

Sau khi ghim method/tools và nhận chính sách scope, đã mở **nguồn/nhãn local** để rà82labels (CV46/JD36):78giữ/4sửa. Hai importance có từ chỉ minimum được sửa thành required; hai quote fragment được mở rộng giữ shared predicate và gắn needsReview. Có11atomic-structure exclusions. Đã nhìn cả2trang PDF nguồn và đối chiếu4JD; không sửa nguồn/runtime/method. Reference cặp có36criterion judgments,14excluded (11structure+2gender/health+1training-offer),22eligible; relevance là quan hệ tài liệu, không score0–100 hoặc quyết định tuyển dụng. ActorCodex/humanreview0. Chỉ78present/4absent một phần, không gold exhaustive; absent của CV dẫn xuất đã bỏ liên hệ không là absence tự nhiên.

Private `final-reference-review/` giữ source review/lineage/labels/subset mới. Validatorlocal **5docs/82fields/4pairs đạt**; provider gate chặn đúng5docs. Nhãn cặp/field đã chuẩn bị không tự đủ confirmed facts/version/revision/fingerprints hoặc quyền gửi model; `final_reference_labels_ready` cho toàn bộ bộ E vẫnfalse. Lịch sử exposure parser/JD giữ nguyên; không nhận đây là final end-to-end mới pristine.

Đã chuẩn bị private `step6/remaining-inputs.md` và mẫu `step6/final-intake/receipt.template.json`: nguồn/group/hash/revision, ngôn ngữ/format/layout, quyền local/provider riêng, lịch sử tiếp xúc, nhãn/actor. Mẫu còn trống, không là tài liệu/consent đã nhận. Không tự tạo CV synthetic hoặc bản dịch rồi gọi là CV gốc Việt.

## 4. Công cụ đánh giá và kết quả kiểm tra

Thêm `scripts/check-stage-d-freeze.py`: kiểm metadata/hash/cấu hình/quyền khai báo và chi phí local, không gọi API/DB. Exit0 chỉ là metadata nhất quán; exit2 là chuẩn bị còn chờ; exit1 là dữ liệu mâu thuẫn/hỏng. Nó không cấp phép gọi provider hoặc thay audit quyền độc lập.

Thêm `scripts/evaluate-final-pair-captures.py`: evaluator final riêng, giữ guard tuning của công cụ cũ. Kiểm đủ nhãn và self-score matrix, đúng source versions/revisions/fingerprint/method/config/top-k/hash; tính lại baseline và rerank theo cùng mẫu số hai chiều, chỉ rerank tập baseline đã chọn. Giữ thứ tự khi hòa điểm, không biến unknown/failed thành 0; usage dùng Decimal, unique jobs, null cho cost chưa rõ, cache tách latency. Phân tích hai chiều từ cùng self matrix không là kiểm chất lượng live recruiter pool với facts công khai khác.

Trong rà công cụ đã xử lý hai lỗi: wrapper có thể ghép capture của version khác; file có thể đổi giữa kiểm hash và parse. Nay binding phải có trong dataset đã ghim và hash/parse cùng byte snapshot, kể cả freeze envelope. JSON trùng key/nonfinite hoặc boolean giả schema cũng bị từ chối.

Từ root, bundled Python chạy `python -m unittest discover -s tests/evaluation -v`: **61/61 đạt**, gồm 16 kiểm tra corpus/capture có sẵn và 45 kiểm tra preflight/final evaluator. Test chỉ dùng fixture tổng hợp/thư mục tạm; không phải kết quả chất lượng corpus thực. Log/kết quả ghim private `step6/evaluation-tests.log` và `verification-summary.json`.

Chạy preflight trên manifest thật: **valid=true, ready=false, errors=[]**, exit2; owner scope policy đã true và4candidate group-pairs có nhãn, còn11 reason codes quy về final data/independence/references/rights/search/funds. Nó kiểm114runtime/27test/23method files/2DLL/12metadata docs/5finalcandidates/4labelledpairs. Negative CLI trên prepared envelope bị chặn `final_freeze_required`, exit2, không mở input evaluator hoặc tạo ranking report. Đây là kiểm guard chuẩn bị; nguồn được đọc riêng để gán nhãn, chưa chạy đánh giá E.

## 5. Chi phí có thể dự trù

Ledger phát triển không đổi: 0,03/2 USD, phần còn lại được guard 1,97 USD; không có unknown AI/reservation mới. Đây không là kiểm số dư live/ngân sách tháng mới. Giá kế hoạch cache-miss/peak `deepseek-flash` là 0,30 USD/M input và 1,20 USD/M output; alias/version ghi theo ngày kiểm tra, có thể thay đổi. [Nguồn giá chính thức DeepSeek](https://api-docs.deepseek.com/quick_start/pricing/).

Với input limit16.000 và output2.048, dự trù bảo thủ một attempt0,00725760 USD; tối đa hai attempts/job0,01451520 USD. Input admission dùng bytes UTF-8 kèm framing, không giả đã đo tokenizer thực. Kế hoạch 24 jobs E (4 extraction +16 cặp +4 writing) tối đa0,34836480 USD; demo mặc định dùng lại evidence/replay0calls, phương án live tùy chọn8jobs tối đa0,11612160 USD. Tổng dự trù tối đa0,46448640 USD, đề xuất giữ trần0,50 USD **trong** 2 USD phát triển đã cấp; chưa allocation hoặc debit ledger.

Search đề xuất4credits miễn phí/0USD, hiện authorized remaining0; cần allowance mới trước query. Email kế hoạch0lượt mới; accepted1/unknown1 cũ giữ nguyên. Nếu phạm vi final/model/pricing thay đổi, dự toán và hash phải được chốt lại trước request. Không mặc định phải làm live demo hoặc gọi lại provider chỉ để tăng n.

## 6. Điều kiện còn thiếu để thực sự hoàn thành bước 6

1. Chính sách phạm vi **đã được owner chốt**; còn chốt pool thực có đủ điều kiện, không bắt buộc đủ4×4.
2. Có nguồn final phù hợp và độc lập cho các claim hiện hành, với coverage/thiếu hụt rõ; không thay bằng synthetic hoặc final lịch sử đã dùng để tuning. Không coi nguồn đã từng được xem sau freeze tự động là đã tuning; audit theo method và claim cụ thể.
3. Có căn cứ và quyền gửi DeepSeek cho đúng tài liệu dùng đánh giá provider. Quyền local hiện có không mở rộng; chưa được cấp quyền mới.
4. Ghim references/exclusions và confirmed facts/bindings của pool thực trước output cuối; đã có4cặp local candidate nhưng chưa ma trận captures/frozen bindings để chấm. Không đòi có kết quả E trong bước6.
5. Chốt phạm vi search và allowance miễn phí mới nếu dùng truy vấn final; đối soát ledger/số dư/ngân sách chung và giá/model trước call, không cấp lại trần theo kỳ/DB.
6. Khi các mục trên đủ, tạo manifest final thực thi được, kiểm preflight rồi lập biên bản M5 ở bước7. Kết quả đánh giá chính thức thuộc E; chưa yêu cầu có kết quả E để đóng chuẩn bị D.

Không còn phần code công cụ/giao thức độc lập đang để dở trong phạm vi lượt này. Những mục chờ là đầu vào/quyết định thật; không đổi tên phạm vi để báo bước6/M5 đạt. Thư mục private có checklist tiếp nhận cụ thể, không yêu cầu gửi secret trong chat.

**Tác động:** 0 request DeepSeek/Tavily/Resend, 0 chi phí mới, không sửa 114 runtime/27 backend-test sources hoặc DLL/DB/schema/migration/dependency/tiến trình API/Vite. 12 nguồn/55 frozen entries và file Word riêng của owner giữ hash. Không deploy/commit/push; chưa mở E.
