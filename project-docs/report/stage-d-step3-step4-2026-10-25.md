# Kiểm tra bước 3 và thực hiện pilot local bước 4

Đợt thực hiện 25–26/10/2026, chốt ngày 26/10, Asia/Saigon. Theo yêu cầu kiểm lại bước 3 rồi hoàn tất các điều kiện bước 4, kết quả là **bước 3 đạt trong phạm vi đã chốt; bước 4 hoàn thành cho pilot tuning local, kết hợp kiểm tra mới với bằng chứng provider thật cùng bản nguồn đã có**. Bước kế tiếp là 5; chưa thực hiện bước 5 hoặc nghiệm thu M5.

## 1. Audit toàn bộ bước 3

Đối chiếu [checklist bước 3](../tasks/stage-d-to-e-checklist-2026-10-25.md), [biên bản bước 2–3](stage-d-step2-step3-2026-10-25.md) và manifest private. Audit có kiểm tra độc lập bằng agent; người rà vẫn là Codex, không ghi thành human/expert review.

| Điều kiện | Kết quả |
|---|---|
| Đúng phạm vi đã xác nhận | 7 tài liệu tuning/4 nhóm/164 nhãn; giữ 5 tài liệu final riêng |
| Nhãn có nguồn và phiên bản | 161 giữ, 2 sửa, 1 mơ hồ; 14 artifact bước 3 và hash report khớp |
| Giá trị và ngữ cảnh | 157 anchor nguồn đạt; 7 nhãn absent được rà riêng, không suy absent từ trường chưa gán nhãn |
| JD chưa chốt biểu diễn | 7 mục needsReview loại khỏi phép đo cấu trúc chính xác; 1 nhãn mơ hồ nguồn loại khỏi exact field/value |
| Nhãn cặp cho pilot | 4 cặp canonical từ 2 nhóm CV × 2 nhóm JD, 58 đối chiếu yêu cầu |
| Mẫu số và điều kiện loại | 2 sức khỏe không đánh giá, 1 thuật ngữ không rõ, 2 composite React/Nest chưa chốt; còn 53 đối chiếu tham chiếu tiềm năng |
| Validator, quyền và lineage | Local validator đạt; gate provider chặn đúng 7 tài liệu, giữ nguồn/nhãn/quyền/split gốc |

Chưa có AI capture thực trên bộ cặp này nên mẫu số đã đánh giá bằng chứng/điểm sâu là **0**, không phải 53 hoặc 56. Nhãn relevance 0–3 chỉ mô tả quan hệ tài liệu, không là điểm năng lực hoặc quyết định loại ứng viên. Bước 3 đủ để chuyển sang pilot local bước 4.

## 2. Cách chạy và bảo toàn dữ liệu

Parser chạy qua entry point `--parse-document-worker`, trả về trước khi tạo API host. Console harness private tham chiếu DLL có sẵn, không ProjectReference hoặc package mới; restore dùng nguồn NuGet rỗng và tắt audit mạng. Tiến trình dùng allowlist biến môi trường, không nạp `.env` hoặc cấu hình local chứa secret.

DLL ghim SHA-256 `8853252d507a183bfd40742386e3ca1c5d90daec41714fca56a0d1c2c802dfd2`; 114 file nguồn khớp candidate D. Raw parser/JD/writing/baseline output được giữ private trước khi tổng hợp. Không tạo revision được owner xác nhận hoặc job/app CV version giả. Toàn văn parser có thể chứa liên hệ trong bản CV gốc và chỉ dùng private local; nhãn và bằng chứng cặp chỉ phủ phần nghề nghiệp.

Corpus 12 tài liệu vẫn `provider_transfer_allowed=false`. Không mở source text/model output của final; chỉ kiểm metadata/hash bảo toàn. Không khởi động hoặc kết nối API/DB trong lượt này.

## 3. Kết quả thực chạy

| Thành phần | Kết quả | Phạm vi kết luận |
|---|---|---|
| Parser CV | 3/3 PDF, 2 nguồn CV; khôi phục đúng 118/118 giá trị present có nhãn | Khả năng đọc giá trị, chưa là semantic extraction F1; bản dẫn xuất không làm tăng số nguồn độc lập |
| JD jd-atomic-requirements-v3 | 4/4 tài liệu, 103 đề xuất; 103/103 source span khớp, 92 mục cần rà | Input là toàn văn gồm heading/duties/context; 103 không phải số yêu cầu gold hoặc mẫu số precision |
| Writing cv-composition-v1 | 4 trường hợp (2 CV × nhãn vi/en); giữ dữ kiện/nhóm, từ chối 10 plan sai | Payload kiểm soát dựng trực tiếp từ nhãn do thiếu description có nhãn; không là Build từ confirmed revision hoặc output DeepSeek |
| Baseline matching | 2 CV × 2 JD/4 nhãn, top-k=1, chạy hai chiều | weighted-keyword-overlap-v1, cấu hình tracked; không confirmed revision hoặc điểm sâu 0–100 |
| Chủ đề học | Danh mục 21 chủ đề; 47 annotation partial/no_evidence: 15 có chủ đề, 32 chưa hỗ trợ | Dry-run tên chủ đề; chưa kiểm eligibility của pair job thật, chưa search hoặc đánh giá URL |

Writing kiểm khóa không tồn tại, ghép sai kinh nghiệm và đưa dữ kiện kinh nghiệm vào dự án. Trường hợp hợp lệ giữ whole facts. Nhãn vi/en chỉ thay nhãn trình bày quanh dữ kiện gốc tiếng Anh, không dịch/viết lại tự do. Đợt kiểm soát không tạo đề xuất sử dụng mới cần owner accept/lưu; hai workflow synthetic thật đã hoàn thành giữ phạm vi bằng chứng chức năng trước đó.

Đối chiếu 39 nhãn JD: 39/39 anchor khớp sau ánh xạ offset LF/codepoint của nhãn sang raw CRLF/UTF-16 của runtime. Có 25 exact span, 10 runtime span chứa label, 4 label chứa nhiều fragment; 35 nhãn có một span trực tiếp, trong đó 31/32 mục đủ điều kiện cấu trúc. Đây là mô tả alignment, không chứng minh semantic equivalence hoặc F1. Ba file Việt có CRLF nên không so trực tiếp hai hệ tọa độ.

Baseline dùng một đại diện CV mỗi nhóm và đúng JD canonical của nhãn cặp. Quy ước import cố định: dòng đầu không rỗng làm title, phần còn lại làm requirements, description rỗng; không giả là các trường JD đã được recruiter xác nhận.

| Hướng baseline, top-k=1 | Queries | Queries có positive | Recall trên queries có positive | Precision/nDCG trên queries đủ nhãn |
|---|---:|---:|---:|---:|
| CV → JD | 2 | 2 | 1,00 | 1,00 / 1,00 |
| JD → CV | 2 | 1 | 0,50 | 0,50 / 0,50 |

Đủ nhãn cho cả 4 cặp trong pool này. JD Việt ngành vận hành nước không có cặp positive; JD Anh phần mềm có cả 2 CV positive. Recall chiều JD chỉ tính một query, top-1 lấy 1/2 CV. CV đều Anh; ngôn ngữ và ngành nghề lẫn yếu tố nên không tách được chất lượng Việt–Anh hoặc đa ngành. Chỉ dùng các số này làm tuning diagnostic với nhãn Codex.

## 4. Phát hiện và giới hạn

Các kiểm soát chưa phát hiện lỗi sai source span, mất whole fact hoặc ghép sai nhóm trong tập kiểm. JD có nhiều đề xuất cần rà khi truyền toàn văn vào requirements; không tự sửa parser để khớp 39 nhãn chưa phủ toàn bộ nguồn, hoặc gọi output không có nhãn là false positive.

Pilot phát hiện **một lỗi ngữ cảnh của nhãn tham chiếu**: `jd-vi-04/requirements[2].importance` phải là `required`, thay cho `unspecified`, vì mệnh đề nguồn có chỉ dẫn yêu cầu trực tiếp. Đã tạo `field-labels-adjudicated-v2.jsonl`/manifest v2 đủ 164 nhãn, sửa đúng một thuộc tính và metadata lineage; giữ giá trị/status/locator, snapshot bước 3 và nguồn gốc. Validator local đạt 7 tài liệu/164 nhãn/4 cặp; provider gate vẫn chặn đúng 7. Kiểm lại 39 anchor và các thuộc tính trực tiếp sau sửa còn 0 bất đồng ở mục đủ điều kiện. Baseline/writing không dùng nhãn JD này nên không chạy lại các output không bị ảnh hưởng.

Bất đồng relation ở `jd-vi-02/requirements[1]` đã được phân xử: nguồn có lựa chọn “hoặc”, runtime giữ proposal `any_of` kèm `needsReview`; reference `single` là placeholder chưa chốt nhóm học vấn/ngành. Giữ mục này trong 7 structure exclusions, không ép runtime khớp placeholder hoặc biến decomposition chưa chốt thành gold. Hai khác biệt đã được xử lý/phân loại, không còn phát hiện chưa phân loại trong pilot.

Danh mục nguồn học chưa hỗ trợ 32/47 annotation tham chiếu. Đây là giới hạn coverage của danh mục cố định; không tạo URL hoặc giả có kết quả cho mục ngoài danh mục. Chưa thay taxonomy/runtime chỉ từ tập tuning nhỏ này.

Chất lượng DeepSeek extraction/matching/writing trên corpus thực chưa chạy vì quyền gửi vẫn false. Kiểm soát deterministic không chứng minh model chọn đúng dữ kiện thực tế. Native CV Việt, mức đại diện, trạng thái nhãn và final độc lập theo từng phương pháp còn phải được ghi đúng trong phương án nghiệm thu.

Tavily/Resend có kiểm chứng chức năng theo manifest hiện hành, allowance 0 nên không gọi lại. Với điều kiện writing/nguồn học của bước 4, đã kiểm lại hash raw writing capture, bản owner rà và **17 artifact workflow**, cùng hai private version/PDF đã lưu; search journal, owner receipt, cleanup receipt và stub hiện hành cũng khớp. Hai writing synthetic thật và 2 Tavily requests/6 URL được owner rà dùng đúng cùng DLL/source hiện tại. Dry-run topic không thay bằng chứng URL; metadata link probe cũ đã dọn theo retention nên không khẳng định đã mở lại link hoặc xác minh khả dụng hiện tại. Không yêu cầu lặp các workflow đã xong.

## 5. Đối chiếu tất cả điều kiện bước 4

| Điều kiện theo checklist | Bằng chứng và kết luận |
|---|---|
| Pilot đúng phạm vi tuning/quyền | 7 tuning docs local-only; nguồn/nhóm/split và transferfalse được giữ — đạt |
| Lưu output trước sửa | Raw runtime, nhãn v1 và diagnostics trước sửa giữ nguyên; bản v2 có lineage — đạt |
| Có kết quả và giới hạn trung thực | Parser/JD/baseline/writing/topic diagnostics ở mục 3, tách deterministic và provider evidence — đạt |
| Writing không thêm/ghép nhầm trong phạm vi kiểm | 4 controls/10 plan sai bị chặn và 2 output DeepSeek synthetic đã owner rà/accept/PDF/lưu — đạt |
| Nguồn học đúng topic/URL thật | 2 truy vấn Tavily vi/en trả 6 URL, owner rà hai nhóm; 32 topic chưa hỗ trợ được ghi rõ, không tạo URL mới — đạt |
| Xử lý hoặc xác định rõ phát hiện | Sửa một nhãn ngữ cảnh; phân loại grouping chưa chốt và giữ exclusions; ghi giới hạn catalog — đạt |
| Owner rà nội dung sử dụng thực tế | Hai workflow thật đã xong; controls offline không tạo nội dung sử dụng mới — đạt |
| Chi phí/quyền/retention | 0 call mới, không cấp lại quota/transfer; giữ receipt sau dọn metadata — đạt |

**Điều chỉnh nhận định nháp trước đó:** việc giữ toàn bộ bước 4 mở để chờ một lượt DeepSeek mới trên corpus thực đã gộp điều kiện chất lượng đại diện/final của M5 vào bước pilot. Nguyên văn checklist chỉ yêu cầu kết quả/giới hạn trung thực và xử lý hoặc phân loại lỗi trong phạm vi chốt; kế hoạch P16–P19 không bắt chạy thêm provider ở bước 4. Bằng chứng thật cùng source đã có được dùng lại, không tính thành đợt probe mới hoặc chất lượng đại diện. Các thiếu hụt coverage/quyền/phương pháp/final được giữ ở bước 6/M5; không bỏ điều kiện nghiệm thu cuối.

## 6. Bằng chứng và trạng thái bàn giao

Artifacts private ở `.local/stage-d-20261023/step4/`: raw parser/runtime, mapping input, masks/diagnostics v1/v2, nhãn/manifest v2, biên nhận sửa nhãn và tái dùng provider evidence, `step4-exit-criteria.json`, `step3-recheck-and-preservation.json`, `step4-summary.json`, `step4-artifact-manifest.json`. Harness có source, lockfile, lệnh/build log và hash DLL/output. Toàn văn CV không đưa vào Git/tài liệu public.

Đối chiếu lại 12 source hashes, 55 frozen entries/51 đường dẫn, 114 source hashes và DLL giữ nguyên. Không sửa runtime/schema/migration; không chạy lại full backend/E2E/CI. Test D lịch sử 133/9/3/8 giữ phạm vi cũ, không tính thành test mới.

Lượt này **0 provider request, 0 search credit, 0 email, 0 USD**. Không tạo allocation hoặc thay ledger: DeepSeek ghi còn 1,97/2 USD theo đối soát trước, không phải số dư live mới; search/email giữ allowance 0 và unknown email đầu giữ nguyên.

**Bước 4 đã hoàn thành trong phạm vi pilot đã chốt; việc kế tiếp là bước 5: chốt lỗi chặn và bằng chứng kiểm thử cùng bản nguồn.** Chưa bắt đầu bước 5 trong đợt này. M5/chất lượng đại diện/final freeze/E vẫn chưa được nghiệm thu; không deploy, commit hoặc push.
