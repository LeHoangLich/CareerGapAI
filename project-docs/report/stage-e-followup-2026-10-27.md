# Giai đoạn E sau sửa ngày 27 tháng 10 năm 2026

Đã xử lý các lỗi kỹ thuật còn lại, chạy kiểm tra sau sửa, diễn tập và chuẩn bị bản dùng thử lưu bền. **M6 chưa được nghiệm thu** vì chưa có biên bản dùng thử/phản hồi giảng viên được xác nhận. Không tạo phản hồi, ký biên bản hoặc xác nhận chất lượng thay người đánh giá. Chất lượng đại diện M3 vẫn chưa được công nhận.

Kết quả chính của bản sửa: **2/2 CV và 8/8 cặp xử lý thành công**, **159/159 backend, 9/9 E2E, 4/4 frontend** đạt. F1 trích xuất chính xác đường dẫn/giá trị **0,623**; trạng thái hệ thống khớp **34/44** nhãn đủ điều kiện. Rerank không cải thiện baseline. Các con số này thuộc **kiểm tra sau sửa trên tập đã xem**, không thay kết quả thực nghiệm đầu hoặc trở thành final độc lập mới.

## 1. Giữ nguyên kết quả đầu và quyền dữ liệu

[Báo cáo E đầu](stage-e-2026-10-27.md) và artifacts private giữ nguyên: CV 0/2, pair 2/8, writing 4/4 trên DLL `a21e45…`; F1 và so sánh rerank khi đó không tính được. Không bỏ job lỗi, sửa nhãn để làm metric tốt hơn hoặc gọi tập này pristine.

Lượt sau dùng lại đúng **2 CV PDF Anh × 4 JD**, 6 nhóm, 131 nhãn trường và 8 nhãn relevance; 72 nhận định tiêu chí/28 exclusions giữ nguyên. Các fingerprint CV/JD/revision khớp bindings trước đó. Không thêm tài liệu, biến thể hoặc synthetic vào mẫu đo. Quyền gửi dữ liệu chỉ áp dụng đúng sáu tài liệu đã được rà; bộ synthetic dùng demo tách riêng.

Clone DB vào database riêng, giữ 14 jobs/chi phí cũ làm lịch sử. Không reset kỳ hoặc hạn mức để cấp lại lượt. Trong lượt sửa có 22 jobs backend qua các phiên bản trung gian; phiên bản cuối có đúng 10 jobs thành công. Những lần trung gian thất bại vẫn nằm trong journal, báo cáo chi phí và manifest.

## 2. Nguyên nhân và thay đổi

- **CV bị cắt đầu ra:** trần 2.048 token không đủ cho một trường hợp. Cho cấu hình full CV riêng tối đa 8.192; prompt nêu rõ độ dài từng trường, yêu cầu đoạn liên tục và nhóm nguồn. Không tăng trần skills/writing mặc định hoặc tự bật provider. Phương pháp CV mới mang policy `cv-fields-bounded-v2`.
- **Matching viện dẫn không hợp lệ:** chẩn đoán giữ raw private đã tái hiện model dùng dữ kiện không có lexical support. Phương pháp `candidate-requirements-v4` chỉ đưa AI các yêu cầu có dữ kiện ứng viên hợp lệ, kèm mã fact và trạng thái được phép. Các yêu cầu không có dữ kiện hợp lệ hoặc thuộc nhóm preference được backend điền `no_evidence` theo guard sẵn có. Đây là phương pháp kết hợp xử lý xác định và LLM, không tuyên bố LLM đã đánh giá mọi tiêu chí. Không biến claim sai của model thành claim đúng; claim sai vẫn bị từ chối. Điểm/trọng số và kiểm quote nguyên vẹn không được nới lỏng.
- **Sai kích thước sau đọc JSONB:** admission đo JSON gọn nhưng PostgreSQL định dạng lại khi đọc. Worker mới serialize đúng payload sẽ gửi và kiểm byte trước `StartAttempt`, tránh tạo attempt tính phí cho lỗi input tại local. Policy mới có config/hash riêng; job cũ tiếp tục dùng prompt cũ.
- **Gợi ý nhầm marketing:** thay token rời bằng nguyên cụm alias có biên từ và chuẩn hóa dấu. `giao tiếp` không còn khớp `tiếp thị`; Java/JavaScript và SQL/PostgreSQL cũng được tách đúng. Kiểm qua API thật trả communication, không có marketing cho trường hợp đã phát hiện.
- **Chẩn đoán lỗi:** `Ai:PrivateDiagnostics` mặc định tắt, chỉ có tác dụng trong Development khi bật rõ. Raw bị từ chối nằm ngoài webroot dưới `.local/ai-diagnostics`, không có endpoint public, không ghi body vào log. CreateNew giữ nguyên phản hồi đầu của từng attempt. Production/Testing không ghi dù cấu hình bật. Đây không phải cơ chế lưu toàn bộ response mặc định.
- **Phản hồi U5:** phiếu trong workspace có góp ý thông báo khi AI tắt còn chung chung. Đã thêm thông báo rõ AI đang tắt, chưa tạo yêu cầu mới và vẫn dùng được CV/thao tác thủ công. Browser synthetic/Fake xác minh HTTP503 `ai_disabled`, thông báo hiển thị, tải file200 và phiên đăng nhập còn hoạt động; không gọi provider.
- **Đóng gói:** loại thư mục phát sinh `frontend/%SystemDrive%` khỏi Git và Docker context; giữ nguyên thư mục đó và file Word tiến độ của người dùng. Không nâng dependency hoặc thêm migration.

## 3. Kết quả đo sau sửa

CV evaluator giữ protocol exact path/value trước chỉnh sửa người dùng: 2 tài liệu scored, **43 TP, 11 FP, 41 FN**, precision **0,7963**, recall **0,5119**, F1 **0,6232**. Có 11 nhãn absent đúng trong tổng 95 nhãn CV. Phép đo nhạy với chỉ số mảng: khác thứ tự kỹ năng có thể thành wrong-value/missing ở đường dẫn cụ thể. Không diễn giải mọi FP là dữ kiện bịa; validator vẫn yêu cầu value/quote nguyên văn nguồn.

So với nhãn do Codex rà, **34/44 trạng thái** khớp; quote và provenance ở kết quả hợp lệ khớp nguồn. Đây là đối chiếu tham chiếu nội bộ trên mẫu nhỏ, không phải accuracy do chuyên gia xác nhận. Không đổi 28 exclusions, tiêu chuẩn nguồn hoặc mẫu số theo kết quả.

Rerank chỉ sắp lại tập top-3 baseline đã chọn, giữ thứ hạng baseline khi điểm bằng nhau:

| Hướng | Chỉ số | Baseline | Sau rerank |
| --- | --- | ---: | ---: |
| CV→JD, n=2 query | Precision@3 | 0,6667 | 0,6667 |
| CV→JD, n=2 ideal khác 0 | nDCG@3 | 1,0000 | 0,9170 |
| JD→CV, n=4 query | Precision@3 | 0,5000 | 0,5000 |
| JD→CV, n=2 ideal khác 0 | nDCG@3 | 1,0000 | 1,0000 |

Recall trên query có positive vẫn 1,00 ở cả hai hướng. Pool JD→CV chỉ có 2 CV; không thể dùng top-3 để chứng minh lợi ích loại ứng viên. **Chưa có bằng chứng LLM cải thiện baseline**; nDCG chiều CV→JD giảm trong lần kiểm này. Công cụ metric cuối gốc không bị đổi để bỏ kiểm final independence; lần phân tích sau sửa gọi lại các hàm metric thuần, ghi scope riêng là regression.

Writing không đổi phương pháp dữ kiện, dùng lại bằng chứng thật 4/4 ở E đầu với giới hạn đã nêu, kèm hồi quy hiện tại. Đã diễn tập thêm bằng browser trên **synthetic/Fake**: nạp nguồn đã xác nhận, duyệt đề xuất, xem PDF, lưu đúng một version mới; bản cũ và public selection giữ nguyên. Đây là fixture tự động, không phải owner preference hoặc giảng viên dùng thử. PDF sinh ra có một trang, copy text được, không ký tự thay thế và đã xem bản render.

## 4. Nguồn học và credits

Sau sửa đã có gap Docker thực trong kết quả thành công. Chạy **2 Basic requests**, Việt và Anh, mỗi request 3 links và journal ghi 1 credit. Git hiện không còn gap đủ điều kiện ở case đã chọn nên không gọi thêm. Đóng hai credits chưa dùng trong cap 4 mới; không hoàn lượt hoặc reset hai credits D cũ.

Đã rà 4 URL duy nhất từ 6 kết quả: hai YouTube bị công cụ đọc trả 429, một YouTube chỉ đọc được trang khung/footer, một bài Medium đọc được nội dung. Không coi lỗi đọc là bằng chứng phải trả phí, cũng không khẳng định mọi nguồn miễn phí. Nhóm truy vấn Việt trả tiêu đề tiếng Anh; ngôn ngữ video/toàn trang chưa được xác minh. UI hiện ghi đúng đây là **ngôn ngữ truy vấn**, không đảm bảo ngôn ngữ tài liệu. Chưa công nhận chất lượng nguồn học tiếng Việt từ lượt này.

Usage API Tavily trước/sau vẫn báo plan usage 2/1000, paygo usage 0, chưa phản ánh hai lượt mới. Giữ **debit 2 credits mới** theo response/journal, không dùng số tổng chưa đổi để hoàn credit hay gọi thêm. Mốc D đã có owner Dashboard review riêng; không lấy review cũ làm xác nhận tổng mới. Chưa quan sát Dashboard mới bằng tài khoản owner trong lượt này. Metadata kết quả giữ private tối đa 7 ngày cho rà soát; không cache raw page/snippet trong DB.

## 5. Chi phí và attempt chưa đối soát

Hai diagnostic calls và 25 attempt backend có response tiêu upper bound **0,03773130 USD**, ghi nợ bảo thủ **0,04 USD**. Tổng 26 attempt backend có thêm một `outcome_unknown` từ phiên bản trung gian: input bị chặn local sau khi đã tạo attempt, trước HTTP, giữ reservation **0,00971520 USD**. Không sửa lịch sử thành 0 hoặc tự hoàn; giữ thêm **0,01 USD** dự phòng ở ledger toàn dự án. Code cuối đã chuyển preflight lên trước StartAttempt để tránh lặp lỗi này.

Tổng phát triển: **spent 0,11 USD + holds 0,03 USD, guarded remaining 1,86 USD** trong trần 2 USD. Allocation sau sửa đã đóng, không có worker DeepSeek chạy nền. Đây là kế toán bảo thủ theo [giá DeepSeek đã đối chiếu](https://api-docs.deepseek.com/quick_start/pricing/), không phải hóa đơn hoặc quy đổi CNY. Email mới 0. Tavily mới 2 credits trong phần miễn phí, cap chi tiền 0 USD.

## 6. Kiểm chứng bản bàn giao

| Hạng mục | Kết quả mới |
| --- | --- |
| Backend với PostgreSQL thật | **159/159**, gồm guard private diagnostics, cụm alias, legacy prompt, grounding và mọi hồi quy cũ |
| Frontend | **4/4**, lint/typecheck/build đạt; có kiểm rõ thông báo AI tắt |
| Browser E2E | **9/9** trên DLL mới, provider Fake, data synthetic |
| Writing qua browser | Duyệt → PDF → version mới đạt; các version cũ/public selection nguyên vẹn |
| Restore current DLL | **19 migrations, 43 bảng, 11 objects, 3 ledger entries**, hash trước replay khớp |
| Quyền sau restore | Cookie cũ401, pending403; fresh login đọc CV/file/job khớp |
| Docker Linux | Build mới và 3 HTTP smoke đạt; image giữ local, không push |
| Demo lưu bền | Chuyển khỏi PostgreSQL test tmpfs sang DB riêng trong container persistent, đối chiếu 43 bảng và replay ledger trước mở |

DLL Windows đo sau sửa ở `.local/stage-e-followup-20261027/build-v4/bin/CareerGap.Api/debug/CareerGap.Api.dll`; Linux build là artifact khác, không gán hash hai nền tảng cho nhau. Bằng chứng E/M5 cũ giữ riêng. Các thay đổi Docker/Git ignore sau đo chỉ tác động đóng gói; source C# của DLL đã kiểm không đổi. Dependency giữ nguyên. Gói source sau sửa đã được giải nén vào thư mục sạch trên cùng máy Windows và kiểm restore khóa phiên bản, backend build, npm ci, typecheck và Vite build; dùng cache dependency hiện có. Không tuyên bố đây là thử trên OS mới hoặc kiểm S3/production.

Giữ vận hành 90/7/14 ngày và failed-file manual queue 7 ngày đã kiểm ở E đầu; schema/worker lưu xóa không đổi. Raw chẩn đoán mới có inventory private và hạn rà/xóa 7 ngày, mặc định không bật trên bản demo. Không cài scheduler hoặc giả đã đảm bảo SLA khi máy tắt.

## 7. Dùng thử và nghiệm thu cuối

[Bộ dùng thử hiện hành](../handoff/stage-e-fixed-20261027/README.md) có hai role synthetic private, CV/JD, proposal Fake chờ duyệt, hướng dẫn U1–U5, phiếu Word mẫu trống, slide và video mới. Phiếu cũ trong workspace có ghi chép U1–U5 nhưng chưa có người đánh giá/ngày/xác nhận; U1 chưa xác nhận thấy đủ trường để đối chiếu. Giữ nguyên bản này ngoài commit và lưu bản private, không gán cho giảng viên hoặc xem là đã nghiệm thu M6. Góp ý U5 về thông báo AI tắt đã được sửa và kiểm lại; không yêu cầu người dùng lặp lại các ghi chép đã có. Không yêu cầu owner lặp lại các kiểm tra D đã hoàn thành. Bản code/commit được ghim bằng biên nhận private khi chốt bàn giao; không commit file Word tiến độ hoặc secret, không push/deploy.

**Phần còn cần người thật:** giảng viên/người đánh giá thực hiện các tác vụ và ghi phản hồi/diễn tập thật. Chủ dự án có thể gửi đường dẫn private của phiếu đã điền để đối chiếu. Kết quả F1/ranking và coverage trên mẫu nhỏ phải được trình bày khi nghiệm thu; chưa có ngưỡng chất lượng được người đánh giá chấp nhận, không tự đặt ngưỡng để báo đạt. Việc Dashboard Tavily chưa khớp được ghi là phần đối soát còn mở, giữ cap/debit thay vì gửi thêm request.

Phần thực thi và hồ sơ kỹ thuật E đã hoàn tất trong phạm vi trên; **M6 còn mở**, không thay bằng quyền ủy nhiệm hoặc phản hồi tự tạo. CV Việt gốc/JD Việt CNTT, nhãn human và chất lượng đại diện vẫn là giới hạn, không được bù bằng synthetic. [Manifest sau sửa](../evaluation/stage-e-followup-2026-10-27.json).
