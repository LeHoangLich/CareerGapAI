# M3 — Kiểm chứng local, bổ sung DOCX và sửa parser JD

Ngày 21/10/2026, tiếp tục theo yêu cầu làm tất cả phần có thể để nghiệm thu M3. **Đã bổ sung DOCX gốc, chạy đo parser trên corpus ngoài synthetic, sửa lỗi JD có bằng chứng và kiểm tra lại luồng sản phẩm. Chức năng local đạt các ca đã chạy; chưa đủ cơ sở công nhận chất lượng đại diện Việt–Anh.**

HEAD vẫn `cca4378ea6752e078de389804a4ee64fdae11f75`; factory JD, test và tài liệu trong working tree chưa commit/push. Không gán CI lịch sử cho bản này. Evidence private: `D:\CareerGapAI\.local\m3-followup-20261021\`. Không triển khai C, deploy hoặc gọi DeepSeek mới.

## 1. Corpus hiện có và đầu vào còn thiếu

Đã bổ sung DOCX/PDF nguyên bản từ [blckclov3r/resume](https://github.com/blckclov3r/resume), repository trực tiếp liên kết hai file và [MIT license](https://github.com/blckclov3r/resume/blob/master/LICENSE). Snapshot commit/license/README/hash được lưu private; không thực thi mã nguồn tải về. DOCX không có macro; ba external relationships không được parser tải về. Không in hoặc đưa danh tính, liên hệ hay mã chứng chỉ vào nhãn/báo cáo.

| Hiện có trong `corpus-v2-ready.json` | Số lượng và giới hạn |
|---|---|
| CV | **6 file / 4 nguồn**: 3 PDF dẫn xuất, 2 PDF gốc, 1 DOCX gốc; đều tiếng Anh |
| JD | **8 tài liệu / 6 nhóm**, 5 Việt + 3 Anh; giữ nhóm gần trùng của các tin hệ thống nước |
| Nhãn | **292**: 279 present, 11 absent, 1 ambiguous, 1 unreadable. Nhãn dự án do Codex chuẩn bị/tự rà, **human review pending** |
| Tuning/final | 9/5 tài liệu, 5/5 nhóm; CV tuning 5 file/3 nhóm, final 1 file/1 nhóm. Không đổi split để làm đẹp kết quả |

Bản cũ [12 tài liệu/246 nhãn](m3-inputs-2026-10-21.md) được giữ nguyên. V2 thêm 46 nhãn của nguồn mới; một giá trị không biểu diễn được nguyên vẹn bằng đối chiếu text PDF được giữ unreadable, không tính negative. 11 absent cũ vẫn gồm 9 field danh tính/liên hệ cố ý loại và 2 field chứng chỉ không có trong phần nghề nghiệp giữ lại; không coi là 11 vắng tự nhiên trên bản gốc.

`corpus-v2-ready.json` sửa tham chiếu hash nhãn kế thừa của manifest v2 và metadata **42 locator JD** về đúng requirements scope/UTF-16/raw newline. Không sửa value/status, structured gold, source file, nhóm hoặc split; không tính lại điểm bằng nhãn sửa sau final. Bản dùng thí nghiệm và hash đóng băng vẫn được giữ để tái lập. Adapter xuất `cv-dataset-tuning.json` và `cv-dataset-final.json`; có README/bảng rà nhãn mới trong thư mục private.

Đã tìm thêm nguồn native Việt qua web và metadata GitHub; các truy vấn repository `cv vietnamese`, `resume vietnamese`, `cv-vietnamese` lần lượt trả 42/18/3 kết quả, có trùng và nhiều kết quả không phải résumé. Những nguồn CV phù hợp đã kiểm tra không có grant dùng dữ liệu đủ rõ; không tải CV chỉ vì public, không coi license của phần mềm/mô hình là license corpus. **Chưa tìm được CV tiếng Việt hoàn chỉnh có quyền phù hợp trong lượt này**; không thể tự tạo sự đồng ý, dịch CV rồi đổi thành native, hoặc ký rà nhãn thay người khác.

Mọi tài liệu intake mới vẫn **chỉ local evaluation, provider transfer false**. Không dùng lại quyền probe synthetic để gửi CV bên ngoài. Key và trần DeepSeek 2 USD đã có; không hỏi lại hoặc cấp lại ngân sách. Không có khoản ghi nợ mới trong lượt này; không đối chiếu số dư/lời gọi ngoài ứng dụng ở lượt không dispatch này.

## 2. Đo parser CV thực sự đã chạy

Chạy assembly parser hiện có trong process riêng, môi trường được lọc và deadline, trên đúng 6 file có hash/nhãn. **6/6 file đọc được; 205/205 giá trị present đã gán nhãn xuất hiện nguyên văn trong các segment**. Không mất giá trị nào trong phạm vi 205 nhãn này. Raw parser giữ private, console chỉ đếm.

Đây là **labelled-value recovery của parser**, không phải precision/recall/F1 trích xuất ngữ nghĩa, liên kết đúng mọi item hoặc bằng chứng DeepSeek đọc đúng CV thực tế. Không chấm field chưa nhãn, 11 absent hoặc unreadable thành đúng. Ba bản dàn lại không chứng minh layout nguyên bản; DOCX/PDF cùng một người không làm tăng số nguồn độc lập.

Đã replay lại 6 captures DeepSeek synthetic cũ bằng validator hiện tại: TP 122/FP 0/FN 0 như trước. Replay không gọi provider, không phải dữ liệu ngoài synthetic, latency hay chi phí mới.

## 3. JD: lỗi có bằng chứng và thay đổi triển khai

Bộ tách mới là **`jd-atomic-requirements-v3`**, **schema vẫn 2**. Không migration hoặc sửa revision/snapshot cũ. Khi yêu cầu extract lại, endpoint vốn có tạo revision mới nếu parser version khác; xác nhận revision cũ vẫn giữ parser snapshot của parent.

- Tiêu đề required/optional tường minh áp dụng cho các item bên dưới; tiêu đề không thành requirement. Mục Markdown khác kết thúc ngữ cảnh trước đó. Tiêu đề chung như “Yêu cầu” không tự áp một quyết định nghiệp vụ mới về importance cho mọi dòng.
- Bỏ bullet/numbering khi tạo item nhưng giữ offset trích dẫn đúng; không chẻ dấu phẩy trong ví dụ ngoặc thành nhiều công nghệ bắt buộc. Giữ `and/or` thành nhóm cần rà; “1 năm hoặc cao hơn” là ngưỡng, không phải hai lựa chọn.
- Câu vừa nêu số năm vừa nói không cần kinh nghiệm trong cùng clause trả **minimumYears null + needsReview**, thay vì phát hành một số năm mâu thuẫn. Thí nghiệm tuning có một trường hợp v2 ghi 1 năm, v3 giữ null và cảnh báo. Nhiều mốc năm trong cùng lựa chọn cũng không bị ép thành một mốc chung.
- Nhận diện “là một lợi thế”, “preferably”, tiêu đề nice-to-have. Câu còn liên từ/nhóm ví dụ hoặc số năm mơ hồ được giữ **needsReview**, không tự coi đã nguyên tử/ngữ nghĩa đúng hoàn toàn.

Thay đổi nằm ở `JobRevisionFactory.cs`; thêm **11 ca kiểm tra** trong `JobRequirementParsingTests.cs`, gồm nguồn CRLF/Unicode, tiêu đề, phủ định, ngưỡng so sánh, nhóm lựa chọn/ví dụ, số thập phân và xác nhận revision có parser snapshot cũ. Không đổi API/DTO, quyền, score L09 hoặc rubric P10.

## 4. Đo JD trước sửa và giới hạn kết quả

So v2 nguyên bản từ HEAD với v3 trên **cùng requirement-section inputs/hash/nhãn**. Harness private gọi trực tiếp factory, không tái triển khai parser bằng Python và không gọi AI. V2 được biên dịch từ snapshot source HEAD dưới tên riêng để không reset working tree. Source/method/dataset được khóa **trước lần xem output final đầu tiên**; không chỉnh parser sau kết quả final.

| Phép đo chẩn đoán | Tuning v2 → v3 | Final v2 → v3 |
|---|---|---|
| Nhãn present có một source span phủ đủ quote | 34/38 → **35/38** | 33/36 → **33/36** |
| Importance đồng ý với nhãn dự thảo, trên phần có thể đối chiếu | 20/32 → **32/33** | 15/27 → **25/26** |
| Relation đồng ý, trên phần có thể đối chiếu | 23/32 → **33/33** | 14/27 → **26/26** |
| Prediction phủ nhiều gold atoms nhưng thiếu cờ review | 3 → **0** | 1 → **0** |
| Số năm tường minh đủ điều kiện đối chiếu | 1/1 → **1/1** | 2/2 → **2/2** |

Tuning có 4 tài liệu/39 nhãn, loại 1 ambiguous; final có 4 tài liệu/36 nhãn. Một mốc năm có scope/phân mảnh không vào phép so sánh, không gọi tất cả bốn mốc đã đúng. Denominator thuộc tính thay đổi vì alignment và loại needsReview; không cộng thành accuracy tổng thể hoặc so các tỷ lệ như thử nghiệm thống kê độc lập.

**Không báo semantic precision/recall/F1:** nhãn partial do Codex, alignment theo source interval không chứng minh requirement nguyên tử đúng. V3 còn 3 gold spans phân mảnh ở mỗi split và 4 output spans phủ nhiều atoms ở mỗi split, đều có cờ review; không tuyên bố parser hiểu hết JD tự nhiên. Mỗi split còn một bất đồng importance với nhãn chưa được người rà: ngữ cảnh yêu cầu chung/tối thiểu cần thống nhất lại khi rà nhãn. Giữ nguyên kết quả final, không sửa gold hoặc code sau khi nhìn điểm để làm pass.

Một lượt đo sơ bộ bị lệch offset do Python Windows đổi LF thành CRLF; các artifacts đó đã **invalidate riêng**, không dùng số liệu trong bảng. Input thí nghiệm hợp lệ ghi byte LF tường minh, offset theo **UTF-16 code units** như C#; source evidence của cả hai parser được kiểm tra lại bằng substring gốc. Có raw output/hash và scripts private để tái lập.

## 5. Hồi quy và luồng sản phẩm chạy mới

| Kiểm tra trên bản hiện tại | Kết quả |
|---|---|
| Backend PostgreSQL 16.10, Fake/key process rỗng | **94/94**, 0 fail/skip, khoảng 48 giây; `m3-backend-final.trx` |
| Corpus/capture Python tools | **16/16**, 0 lỗi |
| Playwright đầy đủ | **9/9**, 0 skip/fail/flaky, khoảng 47 giây; gồm full CV review/reload/confirmation, JD review, builder recovery và đúng application snapshot |
| PDF E2E | Bản dài **2 trang**, bản ngắn **1 trang**; text/geometry/page counter đạt, render và xem đủ ba trang, dấu Việt rõ, không chồng/cắt chữ |
| Corpus và adapter | 14 tài liệu/10 nhóm/292 nhãn hợp lệ; CV tuning 5 file/3 nhóm và final 1 file/1 nhóm xuất được |
| Model/database | Áp **15 migrations có sẵn** vào DB E2E mới; không migration/schema change |

Một lượt backend trung gian 93/93 đã qua trước khi thêm ca/cờ conjunction cuối; **không cộng** nó hoặc 9 targeted đầu tiên vào 94 của bản cuối. Frontend source không đổi; không chạy lại lint/typecheck/unit/build frontend hoặc Docker image build và không gán kết quả lịch sử là lần chạy mới.

PostgreSQL riêng `careergap-m3-20261021`, chỉ bind loopback 55437, tmpfs; integration tests tạo/drop `careergap_test_*`. E2E dùng database riêng `careergap_m3_e2e`, storage và tài khoản synthetic mới. Không đọc/reset DB ứng dụng. Ba job E2E kết thúc succeeded dưới Fake; không cấp key thật cho môi trường này.

Đã dừng đúng API/Vite của lượt này và container riêng, không còn listener thuộc các process đó, xóa file mật khẩu demo tạm và khóa helper môi trường khỏi tái sử dụng vô tình. Không có deploy, push, provider call hoặc thu thập dữ liệu qua tài khoản người dùng. Sổ ngân sách cũ giữ nguyên.

## 6. Kết luận nghiệm thu theo từng phần

| Phần M3 | Kết luận hiện tại |
|---|---|
| Hợp đồng/provenance/version và xác nhận | Đạt các ca local đã chạy; giữ source snapshot và phân biệt user_statement |
| Parser/file lỗi, worker/quota/restart | Hồi quy đạt; thêm đọc corpus ngoài synthetic, không hứa mọi layout/OCR hoặc OS crash trong call thật |
| simple-v1, PDF, ứng tuyển/snapshot | Đạt 9 E2E và PDF QA trên synthetic; không chờ template khác |
| Đánh giá ban đầu trước sửa | Có replay DeepSeek synthetic, parser ngoài synthetic và JD diagnostics có error analysis/tập giữ lại |
| Chất lượng đại diện Việt–Anh | **Chưa đủ bằng chứng**: CV native Việt còn thiếu, nhãn chưa được người rà, chưa đủ quyền transfer để đo DeepSeek trên corpus ngoài synthetic |

Các phần local độc lập vừa triển khai đã được kiểm tra xong; giới hạn parser ở mục 4 vẫn được giữ công khai. M3 tổng thể còn mở ở chất lượng đại diện, không phải thiếu key hoặc ngân sách đã cung cấp. Để chốt chất lượng thực tế cần tài liệu native Việt có quyền, người rà nhãn/quy tắc còn mơ hồ và quyền provider phù hợp cho đúng tập gửi; không thể thay các quyết định/xác nhận đó bằng thao tác của agent. Không tự đặt ngưỡng hoặc tuyên bố giảng viên đã duyệt. [Checklist hiện hành](../phase-03-acceptance.md).
