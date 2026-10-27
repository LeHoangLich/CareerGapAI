# Giao thức đánh giá chuẩn bị ở bước 6

Phiên bản `stage-d-final-protocol-v3-local-intake`, ngày26/10/2026, Asia/Saigon. **Owner đã chốt chính sách phạm vi có điều kiện; đã ghim triển khai và giao thức, chưa freeze bộ thực nghiệm cuối hoặc chạy E.** Bảnv3 bổ sung candidate/source review và hướng dẫn chuyển việc, không đổi thuật toán/prompt/rubric/trọng số/phép đo từv2. Snapshotv2 cùng manifest trước giữ trong privateentry; manifest hiện hành followup ghim hash mới. Tên file hoặc cờfinal không tự tạo quyền sử dụng.

**Bổ sung local hiện hành:** candidate2CVAnh×4JD/6groups/131fieldlabels/8pairreferences. Một nguồn CVAnh mớiCC0 được nhận và tạo derivative nghề nghiệp riêng; không đổi corpus12đã chốt, không bùCVViệt hoặc đếmPDFvariant nhưnhóm mới. Draftfacts64/42,8pair/4writing controls và106fieldshape đã kiểm trên DLL5; chưa appconfirmedversions/revisions hoặc providerpermission. Xem [biên bản followup](../report/stage-d-step6-followup-2026-10-26.md) và [việc owner cần làm](../tasks/stage-d-step6-owner-actions-2026-10-26.md). Các số1CV/4cặp ở phần inventory gốc dưới đây là phạm vi corpus12lịch sử, không thay bộ mở rộng.

**Quyết định owner hiện hành:** 4 nhóm CV ×4 nhóm JD là mục tiêu có điều kiện, không là mức tối thiểu bắt buộc. Chốt pool theo nhóm độc lập thực có đủ nhãn và quyền; nếu nhỏ hơn báo đúng phạm vi/giới hạn, không dùng biến thể hoặc synthetic để bù. Ưu tiên CNTT, CV/JD Việt/Anh và báo coverage từng ngôn ngữ. Giữ k=3 và trần dự toán đề xuất0,50USD; chưa cấp phép chi/call hoặc mở quyền provider. Bộ corpus hiện tại vẫn local-only. [Lịch sử lựa chọn](stage-d-scope-options-2026-10-26.md) không thay quyết định này.

## 1. Phiên bản và phạm vi kết luận

Dùng source/DLL đã kiểm chứng ở bước 5; giữ candidate cũ và bằng chứng provider lịch sử. Ghim parser, validator, schema, prompt, rubric/weights, baseline options, projection, writing, learning và template theo hash. Mọi thay đổi source/config/dataset/nhãn sau freeze tạo phiên bản mới và rà lại ảnh hưởng tới final.

Cấu hình AI kế hoạch: `deepseek-flash`, input limit 16.000, output riêng 2.048 cho CV/pair/writing, temperature 0, JSON object, thinking disabled, completion phải `stop`. Giữ kiểm schema/source/quote của backend. Ba config hash được tạo bằng serializer và công thức `JobService.EnqueueCore` của DLL hiện tại, là cấu hình kế hoạch, chưa là AiMethod được tạo hoặc provider run. Model version ghi theo tài liệu công khai ngày kiểm tra; alias không phải endpoint version bất biến và chưa có model version trả về được capture. Không tự tăng output budget hoặc đổi alias sau final để làm kết quả tốt hơn.

Source mặc định vẫn Fake; không bật provider trong bước chuẩn bị. Trước call sau này phải đối chiếu giá, số dư/ledger, quyền từng tài liệu và allocation trong trần phát triển chung. [Dự toán kế hoạch](stage-d-evaluation-budget-2026-10-26.json) không cấp quyền chi hoặc giữ chỗ ngân sách.

## 2. Dữ liệu, nhãn và final độc lập

Bộ owner hiện chốt:12tài liệu/9nhóm, chỉ local evaluation;7tuningdocs/4nhóm/164nhãn và5finaldocs/5nhóm/82nhãn đăng ký. Finalcandidate có1CVAnhPDFdẫn xuất,4JDtextViệt/Anh; originalpairlabels0, đã chuẩn bị4reference cặp local riêng sau lock method-tools, chưa pool/bindings/finalfreeze. Tất cảtransferfalse. Bộ followup14tài liệu và23nhãnDOCX đã rà giữ riêng, không cộng vào bộ12hoặc chuyển tuning thành final. HaiJDViệt ngoàiCNTT vàhaiJDAnhCNTT báo riêng, chưaCVViệt; không kết luận hiệu ứngngônngữ độc lập với ngành.

Parser CV/JD final từng có output được xem ngày 21/10. Freeze JD lịch sử vẫn giữ giá trị trong phạm vi cũ; không đổi thành final mới chưa chạm. Baseline/pair/writing/learning chưa có output final được ghi nhận, nhưng điều đó chưa tự chứng nhận độc lập hoặc đủ quyền. Audit theo từng phương pháp, không đánh đồng mọi exposure và không tự xóa lịch sử.

Lập kế hoạch chi phí trên mục tiêu có điều kiện **4 nhóm CV ×4 nhóm JD**; không chặn chỉ vì không đủ con số này, không gọi đó là dữ liệu đã có/cỡ mẫu tối thiểu/mức đại diện đủ/ngưỡng giảng viên duyệt. Pool thực vẫn phải đủ quyền/nhãn và độc lập theo method. Coverage Việt/Anh, định dạng/layout gốc, ngành và khoảng thiếu chốt theo nguồn thực; báo riêng CNTT/ngoài CNTT, không dùng JD Việt ngoài CNTT để chứng minh matching CNTT tiếng Việt. Nếu cần nhóm final mới, thu nhận trước output tương ứng với ID/group/hash, revision, quyền local/provider riêng, exposure log và nhãn/actor. Không tạo CV rồi gọi native hoặc suy quyền cloud từ quyền local.

Mỗi nhóm chỉ có một canonical CV/JD khi tính ranking. Biến thể PDF/DOCX/bản dịch cùng nhóm dùng đo coverage extraction, không thành query/người độc lập. Tuning/final/demo không trùng nhóm. Demo mặc định tái dùng synthetic/workflow receipts hiện có và ghi rõ replay; không gọi lại để tăng số lượt. Không tự công khai CV thực để dựng thí nghiệm recruiter; phân tích ranking hai chiều có thể dùng ma trận self với cùng facts và ghi giới hạn này.

Nhãn gán/rà trước output cần đánh giá, ghim phiên bản/actor/source context. Giữ `unknown`, `absent`, `ambiguous`, `unreadable` riêng; không coi chưa nhãn là relevance 0. Không giả human/expert review hoặc bắt buộc có người thứ hai. Bản tuning v2 sửa một importance giữ lineage; các cấu trúc chưa chốt bị loại đúng mẫu số. Exclusions final phải được ghi riêng theo nguồn thực, không sao chép con số exclusions của tuning để làm gold.

## 3. Capture và nguồn đầu vào bất biến

Lưu raw output/error trước sửa, model/method/config/prompt/parser/parameters, finish reason, source/version/revision/fingerprint, timestamp, cache, attempts, token/cost/unit. Revision confirmed sau sửa hoặc PDF builder không là gold thay raw extraction. Không tạo confirmed revision giả cho corpus chưa được rà.

Trước pair run, ghim canonical source hashes và exact CV/JD version/revision, payload fingerprint cho từng cặp từ dữ kiện đã xác nhận hợp lệ. Dataset baseline có thể giữ metadata bindings bổ sung; final evaluator kiểm capture đúng những bindings này để không chấm nhầm khi tráo capture. Wrapper capture phải giữ nguyên output endpoint, không sửa score/criteria hoặc đổi tên cặp để khớp gold.

Input evaluator gồm freeze envelope và ba file dataset/baseline/captures có SHA-256. Tool kiểm trạng thái/approval/quyền và metadata trước mở input; hash và parse cùng byte snapshot, không đọc lại nội dung sau kiểm hash. Output private không ghi đè input hoặc kết quả đã có. Sai hash/binding/schema, score lỗi hoặc thiếu capture tạo lỗi riêng, không score 0 giả.

## 4. Parser, trích xuất CV và JD

Parser đo đọc được file, thứ tự/source spans, giá trị có nhãn được khôi phục, Unicode, no-text/timeout. Tỷ lệ khôi phục giá trị không là semantic extraction F1.

CV dùng `cv-capture-exact-path-value-v1`: path/value so chính xác theo ordinal, không tự trim/fold/alias để khớp. Trên nhãn present/absent đã được chọn làm reference, dự đoán đúng path/value là TP; dự đoán sai giá trị cho một present tạo FP và FN; dự đoán ở absent là FP; thiếu present là FN. Trường chưa nhãn báo unscored, không suy FP/negative. Pending/ambiguous/unreadable loại khỏi mẫu số tương ứng, báo riêng. P/R/F1 null khi mẫu số không có dữ liệu; giữ scored/failed/missing document counts. Quote/source disagreement tách với value đúng. Báo coverage theo nhóm gốc, ngôn ngữ, format, layout và field group.

JD báo riêng atomic span, importance, minimumYears, relation/alternatives và needsReview. Heading chung không tự thành bắt buộc; marker trực tiếp trong mệnh đề phải được giữ. Offset nhãn Python LF/codepoint và runtime raw CRLF/UTF-16 phải được ánh xạ minh bạch trước đối chiếu. Whole-source stress output không là danh sách gold exhaustive; không suy proposal ngoài nhãn thành false positive. Chưa có JD semantic final evaluator đầy đủ thì dùng packet source adjudication với numerator/denominator rõ, không xuất F1 tổng hợp giả.

## 5. Retrieval và ranking hai chiều

Ghim baseline `weighted-keyword-overlap-v1` và options hash `d32f0fca8fd8a12b36d47c09845c599323f94b18875cd405f4f2f81283a6bb5d`. Normalization/weights/token rules theo đúng source đã ghim; không gộp SQL/PostgreSQL, Java/JavaScript hoặc sửa alias sau final. **k=3 đã được owner chốt**. Khi pool nhỏ hơn, denominator dùng min(3,pool size), công bố pool đầy đủ/không có phép đo chọn tập con ở chiều tương ứng; một CV làm ranking JD→CV suy biến. Không đổi k sau khi xem kết quả.

Relevance 0–3 mô tả quan hệ tài liệu; positive là relevance ≥1. Nó không là điểm năng lực hoặc nhãn score 0–100. Query cần đầy đủ nhãn trên toàn pool để tính precision/nDCG; unknown không thành 0. Ma trận canonical đầy đủ được ghim trước khi chạy.

Final pair evaluator nhận đủ self score matrix, chỉ **rerank tập baseline đã chọn** ở mỗi chiều CV→JD/JD→CV, không thêm cặp ngoài tập đó. Full matrix là chi phí thí nghiệm, không tuyên bố app chấm mọi hồ sơ trong một request. Stable ties giữ thứ tự baseline. Nếu eligible facts giữa self/public khác nhau thì không diễn giải các score là cùng điều kiện; phân tích hai chiều dùng lại self matrix phải ghi rõ đây là phân tích ma trận cùng facts, chưa là kiểm chất lượng recruiter pool trực tiếp.

Quy ước chung cho cả hai order: Recall@k chỉ trung bình trên query có positive; nDCG dùng gain `2^relevance−1`, chỉ trung bình khi ideal DCG>0; query không có positive vẫn có mặt trong số query/coverage/top-1. Precision denominator là min(k,pool size), slot không trả về tính không truy hồi được positive trong pool đã đủ nhãn. Báo denominator từng metric và số zero-positive queries. Công cụ mới tính lại cả baseline theo cùng quy ước, không so trực tiếp aggregate cũ vốn dùng quy ước zero-positive khác.

Thiếu/lỗi score matrix thì dừng comparison, báo failure/missing coverage riêng; không âm thầm bỏ cặp hoặc chấm 0. Replay không phải call hoặc latency mới. Tuning evaluator cũ tiếp tục chỉ nhận tuning; không bỏ guard hoặc đổi split trong file cũ để chạy final.

## 6. Điểm, bằng chứng, writing và nguồn học

Rubric experimental hiện tại: required weight2, optional/unspecified weight1; met1/partial0,5/no_evidence0; preferences không tham gia score. NeedsReview hoặc numeric experience không được tự met. Điểm chỉ có ý nghĩa cặp CV–JD/phương pháp, không xác suất trúng tuyển; AI không thay trạng thái application.

Đánh giá criteria trên reference đã ghim: status, unsupported positive claims trong số met/partial có nhãn, exact quote/fact key/provenance và required chưa có bằng chứng. Loại đúng đơn vị nguồn mơ hồ/grouping chưa chốt; không suy thiếu năng lực từ thiếu thông tin. Không có gold score 0–100 được chốt thì không tính MAE/calibration từ relevance 0–3. Đây là packet adjudication riêng, công cụ ranking không tự đo chất lượng lời giải thích.

Writing chỉ ghép nguyên facts theo cv-composition-v1. Kiểm nguồn, toàn dữ kiện, nhóm kinh nghiệm/dự án, user_statement attribution, target đủ và các claim không có nguồn. Bản dùng thực tế phải được owner xem/chấp nhận trước PDF/lưu; kiểm soát deterministic không thay chất lượng lựa chọn của model. Ghi rõ nhãn vi/en không dịch facts; simple-v1 còn heading Việt trong PDF nội dung Anh.

Nguồn học: chỉ topic từ catalog cố định, level/language; không tên/liên hệ/toàn CV/JD/quote gốc. Chọn topic/no-support đo riêng với search quality. Chỉ URL thật từ search, không dựng URL hoặc fetch tùy ý; ghi snapshot và thời điểm owner rà, ngôn ngữ query không tự thành ngôn ngữ toàn trang. Nhãn relevant/access/login/paid/unknown theo điều thực sự đã quan sát. Search allowance hiện0; đề xuất4credits mới vẫn pending, không có request ở bước6. Metadata review dùng retention hiện hành, dọn sau rà và giữ receipt/hash.

## 7. Usage, lỗi và công bố

Báo requested/admitted/succeeded/failed/missing/rejected riêng để kết quả conditional trên captures thành công không che failure rate. Usage dùng unique job/attempt, phân cache/noncache; các bản export trùng job không cộng chi phí hai lần. Unknown token/cost giữ null và số lượng chưa rõ, không cộng null thành tiền0 hoặc hoàn dự trù. USD dùng decimal; test_credit/search credit/email tách riêng. Latency gồm queue/retry; median và p95 nearest-rank trên mẫu noncache, không gọi là latency provider khi chưa đo HTTP riêng.

Không đưa CV/contact/token/raw private vào Git hoặc report công khai. Báo cáo chỉ counts, giới hạn và ID giả danh phù hợp. Synthetic, Codex reference, owner review, history và final độc lập được ghi riêng. Không tự chọn ngưỡng chất lượng hoặc ghi giảng viên đã duyệt.

## 8. Lệnh và điều kiện trước E

Từ root, chỉ kiểm readiness, không đọc final text hoặc gọi provider:

```powershell
python scripts/check-stage-d-freeze.py project-docs/evaluation/stage-d-step6-preparation-2026-10-26.json
python -m unittest discover -s tests/evaluation -v
```

Manifest chuẩn bị hiện phải trả pending, không phải ready. Công cụ final ranking chỉ chạy sau khi có envelope `final_frozen` với approval/independence/rights, exact config/top-k/bindings và ba input hash đã đủ:

```powershell
python scripts/evaluate-final-pair-captures.py --freeze .local/<final>/freeze.json --output .local/<final>/ranking-report.json
```

Không chạy lệnh thứ hai trên dữ liệu thực trong bước6 hiện tại. CV replay dùng công cụ capture hiện có trên DLL đã ghim, input/capture có quyền và output private; JD/evidence/writing/learning có packet rà riêng. Ready của preflight chỉ xác nhận metadata nhất quán, không là sự cho phép gọi API, mở quyền dữ liệu, nhận xét chất lượng hoặc biên bản M5. Những đầu vào chưa có phải giữ pending trước freeze/cuối E.
