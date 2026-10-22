# CareerGapAI — Đánh giá hiện trạng và kế hoạch tiếp theo

> C ngày 22/10 theo yêu cầu tiếp tục tuần tự: P10–P15 đã triển khai chức năng local và so sánh sơ bộ synthetic. [Report C](../report/stage-c-2026-10-22.md), [hợp đồng API/rubric](../phase-04-matching.md). Chủ dự án chốt JD snapshot lúc nộp; chưa mở GĐ5/deploy. Các đánh giá “chưa thực hiện C” trong phần hiện trạng gốc là lịch sử.

> Tiếp tục M3 ngày 21/10 theo yêu cầu làm hết phần có thể: [biên bản local mới](../report/m3-local-acceptance-2026-10-21.md). Bổ sung DOCX gốc, corpus 14 tài liệu/292 nhãn; JD parser v3, 94 backend/9 E2E đạt. M3 chất lượng đại diện còn native vi CV/người rà/quyền transfer; chưa thực hiện C, không call/phí mới.

> Bổ sung đầu vào theo yêu cầu mới ngày 21/10: [report corpus/nhãn M3](../report/m3-inputs-2026-10-21.md). Có pilot 4 CV/3 nguồn, 8 JD/6 nhóm, 246 nhãn dự thảo; chưa đủ native vi CV/DOCX gốc và người rà nhãn/độ phủ đại diện. Không phí mới, chưa đóng M3 hoặc mở C.

> Yêu cầu mới ngày 21/10: thực hiện nghiệm thu chất lượng M3. [Biên bản kiểm tra](../report/m3-quality-review-2026-10-21.md) ghi validator/replay mới và coverage; chưa đủ bằng chứng đại diện, chờ corpus/nhãn có quyền. Không thêm phí, không đổi ngưỡng và chưa thực hiện C. Ghi nhận tạm hoãn bên dưới là lịch sử.

> Chốt chức năng B ngày 21/10: [probe DeepSeek thật và browser workflow PDF/apply/snapshot](../report/stage-b-provider-2026-10-21.md) đã đạt trên synthetic; trần DeepSeek 2 USD cho toàn giai đoạn phát triển, không reset theo lượt. M3 chất lượng trên corpus đại diện vẫn hoãn, chưa thực hiện C.

> Bổ sung local B ngày 19/10: [report](../report/stage-b-followup-2026-10-19.md), [checklist M3](../phase-03-acceptance.md). Công cụ replay/capture và probe v3 sẵn sàng local; provider/corpus và đánh giá chất lượng chưa có, không tự đóng M3 hoặc bắt đầu C.

> Tiếp tục theo yêu cầu: [báo cáo giai đoạn B — P04/L14–P09](../report/stage-b-2026-10-16.md), [hợp đồng triển khai](../phase-03-extraction-contract.md). Đã chốt một simple-v1 cho GĐ3; phần local đã được kiểm thử, quality/provider còn chờ. Các nhận định ban đầu dưới đây là lịch sử; chưa bắt đầu C.

> Thực thi theo yêu cầu chủ dự án: [báo cáo giai đoạn A — P01–P03](../report/stage-a-2026-10-16.md), [hiện trạng chuẩn](../current-status.md). Các kết quả đánh giá ban đầu bên dưới giữ theo thời điểm; giai đoạn A đã chạy kiểm tra mới, chuẩn bị corpus/provider và ghi riêng phần còn chờ. Giai đoạn A tại thời điểm đó chưa làm B; lượt tiếp theo đã thực hiện B theo liên kết mới ở trên.

Ngày đối chiếu: **16/10/2026**, múi giờ Asia/Saigon. Dự án: `D:\CareerGapAI`.

## 1. Kết luận

**Dự án đã có nền tảng tuyển dụng local tương đối đầy đủ, đang làm một phần GĐ3. Phần AI cốt lõi và thực nghiệm của đồ án còn đáng kể.** Không nên làm lại CRUD hoặc quay lại cấu hình deploy lúc này.

- L01–L09: có implementation các luồng nền tảng, tài khoản, công ty/JD, CV, ứng tuyển, sàng lọc, công khai CV, lưu/xóa và matching từ khóa hai chiều.
- L10–L13: có revision kỹ năng CV, revision yêu cầu JD, chẩn đoán trùng từ và builder/PDF một mẫu. Đây là các lát của GĐ3, chưa phải hoàn thành GĐ3.
- Chưa có chấm phù hợp 0–100, phân tích sâu có bằng chứng, xếp hạng tập đơn bằng AI, AI viết CV, nguồn học hoạt động, quản trị nội dung đầy đủ và kết quả đánh giá trên dữ liệu đại diện có nhãn.
- M1/M2/M3 chưa có biên bản nghiệm thu đầy đủ theo điều kiện roadmap; không đồng nghĩa các chức năng đã xây phải làm lại. Cần đóng từng điều kiện còn thiếu và ghi rõ bằng chứng.
- **Git và CI đã tiến xa hơn tài liệu:** HEAD, origin/main local và main GitHub đều ở `b392ef06f29ad20faacbd97f2bbc118de9de58e5`; working tree sạch. CI đúng commit đã thành công.

Không đưa ra tỷ lệ hoàn thành tổng thể: số lát L01–L13 không có trọng lượng tương đương với phần AI, dữ liệu và đánh giá còn lại.

## 2. Phạm vi và cách đối chiếu

Đã kiểm kê cấu trúc và tệp được Git quản lý: **139 tệp**, gồm 28 tệp C# backend ngoài migrations, 15 migration chính, 11 tệp trong frontend/src, 13 tệp C# test và 7 E2E spec. Đối chiếu các nhóm chức năng với:

1. `AGENTS.md`, README, bốn tài liệu đặc tả/công nghệ/roadmap/database.
2. Task nền tảng, kế hoạch local tiếp theo, hợp đồng triển khai, chính sách lưu/xóa, checklist dịch vụ, dependency và kế hoạch đánh giá.
3. Báo cáo thực thi và các cập nhật L01–L13, hồi quy L01–L09.
4. Endpoints, models/DbContext, parser/storage, worker/LLM, matching, builder và các bài kiểm thử tương ứng.
5. TRX và manifest evaluation đã lưu; Git và GitHub Actions đúng commit hiện tại.

Phân biệt ba loại bằng chứng: **mã đã có**, **kết quả kiểm tra lịch sử**, **kiểm tra vừa chạy**. Không dùng thư mục dependency/build sinh ra để tính tính năng; không mở/in secrets hoặc CV thật. Đây là đánh giá tiến độ và phụ thuộc triển khai, không phải chứng nhận mọi dòng mã không có lỗi.

## 3. Dự án đã làm được gì

| Nhóm | Đã hiện thực | Giới hạn thực tế |
|---|---|---|
| Nền tảng | React/TypeScript/Vite; .NET 10 monolith; EF Core/Npgsql; PostgreSQL; lockfiles; Docker; GitHub Actions | Docker build thành công không chứng minh production chạy được |
| Tài khoản | Đăng ký Candidate/Recruiter, login/logout, đổi mật khẩu, reset token, cookie/CSRF, role/owner và kiểm tra trạng thái tài khoản | Email khôi phục dùng fake mailbox; chưa có bằng chứng giao nhận thật |
| Profile | Candidate quản lý profile; allowlist trường recruiter được xem; concurrency token | UI hiện còn dạng công cụ kiểm chứng, gồm nhập ID để xem profile và hiển thị một phần dữ liệu thô; cần hoàn thiện trải nghiệm |
| Công ty/JD | Một recruiter owner/công ty; tạo/sửa tin thành version; draft/publish/close; tìm/lọc/phân trang; lưu việc | Không phải hệ thống nhiều recruiter cùng công ty; giữ đúng phạm vi đã chốt |
| CV private | Upload PDF text/DOCX; đặt tên; nhiều CV/nhiều version; kiểm tra file/hash; tải theo quyền | Parser còn giới hạn bố cục, header/footer DOCX và thời gian xử lý thư viện đồng bộ |
| Ứng tuyển | Chọn đúng CV version; ghim CV/JD lúc nộp; submission key; chống đơn hiệu lực trùng; history và notification | Snapshot hiện chưa ghim revision dữ liệu trích xuất; cần bổ sung theo ngữ cảnh AI tương lai |
| Sàng lọc | Ma trận submitted/reviewing/shortlisted/rejected; candidate rút đơn; note owner-only; block/unblock theo tin; nộp lại thành đơn mới | Chưa có phân tích/xếp hạng AI cho tập đơn |
| CV công khai | Tối đa một lựa chọn version; publish/hide/replace; recruiter xem bản gốc; candidate khác xem DOCX dựng lại và quét liên hệ | Bản text-only không giữ layout gốc; regex không chứng minh nhận diện mọi cách viết liên hệ |
| Lưu/xóa | Thu hồi khi yêu cầu xóa; hủy trong 7 ngày; purge và object ledger/retry; đơn terminal hết quyền sau 90 ngày; dọn pending 24 giờ | Backup/restore/expire là runbook local thủ công; chưa bảo đảm lịch vận hành 14 ngày |
| Job AI | Job lưu PostgreSQL; lease/generation; retry tối đa một lần; idempotency; reservation/attempt/settlement; fake tách test_credit khỏi USD | Chủ yếu phục vụ extract skills; chưa đủ các loại tác vụ matching/write/search trong thiết kế đích; chưa có quota theo user |
| Matching L09 | CV private của chính chủ tìm JD đang mở; recruiter owner tìm CV public; retrieval_score 0–1; evaluator offline | Đối sánh từ khóa, chưa phải đánh giá ngữ nghĩa hoặc điểm phù hợp 100 |
| Revision CV L10 | Skills schema v1; nguồn/quote/offset; owner xác nhận; user_statement tách khỏi bằng chứng file | Chưa trích xuất đầy đủ kinh nghiệm, dự án, học vấn, chứng chỉ và liên hệ |
| Revision JD L11 | Tách yêu cầu theo dòng, gợi ý required/optional/unspecified; recruiter owner xác nhận | Bộ tách tất định local; dòng ghép nhiều yêu cầu hoặc “A hoặc B” chưa được biểu diễn đầy đủ |
| Diagnostic L12 | Đếm term overlap trên revision đã xác nhận; public scope loại skill tự khai báo; giữ nguyên baseline | Không đổi score, thứ tự hoặc tập L09; không làm tăng recall ngoài tập baseline |
| Builder L13 | Nhập đủ nhóm dữ liệu, preview/download PDF tiếng Việt, lưu/mở lại/sửa thành version mới; simple-v1 | Một mẫu thử; chưa có AI viết; schema v2 user_entered chưa được L12 tiêu thụ |

## 4. Kiểm chứng hiện tại

### 4.1. GitHub/Git vừa xác minh

- Nhánh: `main`; commit `b392ef0`; working tree sạch trước và sau kiểm tra.
- GitHub main được truy vấn trực tiếp, khớp HEAD local.
- [CI run 38836054191](https://github.com/LeHoangLich/CareerGapAI/actions/runs/38836054191): completed/success đúng SHA hiện tại. Cả ba job backend, frontend và docker đều success.
- Workflow hiện có restore/build/test backend, kiểm tra frontend và Docker build. **Chưa có bước chạy Playwright E2E trong workflow này.**

### 4.2. Kiểm tra vừa chạy trong lượt đánh giá

| Kiểm tra | Kết quả |
|---|---|
| Frontend `npm run lint` | Đạt |
| Frontend `npm run typecheck` | Đạt |
| Frontend `npm test` | 3/3 đạt |
| Frontend `npm run build` | Đạt; chunk PDF khoảng 1.199 kB minified, 443 kB gzip; được lazy-load |
| Backend build + test lọc `MatchingEvaluationTests` | 6/6 đạt; output build ở thư mục work riêng |
| Toàn bộ integration PostgreSQL local | Chưa chạy lại: Docker engine hiện không hoạt động |
| Browser E2E local | Chưa chạy lại trong lượt này |

Đã đọc hai tệp TRX lịch sử `foundation.trx` và `milestones-l01-l09.trx`: mỗi lần có **52 total, 52 passed, 0 failed**, hoàn tất sáng 16/10/2026. Đây là bằng chứng thật đã lưu, không phải 52 test được chạy lại trong lượt này. CI mới xác nhận backend test theo workflow tại commit hiện tại, bổ sung cho bằng chứng local lịch sử.

Báo cáo có 5 workflow E2E hồi quy L01–L09 và một workflow builder đạt trong lượt riêng; không cộng các lần chạy này thành một lần chạy 6/6 hiện tại, cũng không coi đủ cả 7 spec. Spec foundation cũ còn yêu cầu demo credentials.

### 4.3. Chất lượng AI

Chưa có bằng chứng chất lượng AI trên corpus đại diện có quyền sử dụng/nhãn được rà soát. Manifest hiện tại là synthetic smoke; metric recall synthetic không thể dùng làm độ chính xác dự án. FakeLlmClient chỉ dò danh sách tám kỹ năng mẫu; adapter DeepSeek hiện cũng chỉ có hợp đồng extract skills. Search fake trả danh sách rỗng; email fake chỉ ghi outbox.

## 5. Các điểm tài liệu cần đồng bộ

| Vấn đề | Đối chiếu | Cách xử lý |
|---|---|---|
| Nhiều tệp vẫn ghi HEAD/origin=5f4defe, working tree chưa commit/push/CI | Hiện b392ef0 đã ở GitHub main và CI success | Thêm khối hiện trạng duy nhất gắn SHA/ngày/run; giữ các mục báo cáo cũ với nhãn lịch sử |
| Mở đầu roadmap dừng ở L01–L09/13 migrations/47 tests | Mã có L10–L13 và 15 migration; TRX ghi 52 | Cập nhật overview; không bắt người đọc ghép nhiều phụ lục để biết hiện trạng |
| Roadmap mục quyết định còn nói worker/purge chưa triển khai | L08 code/test/report đã có local lifecycle và restore ledger | Ghi “đã triển khai local, vận hành/backup scheduling còn thiếu” |
| Hợp đồng triển khai vẫn có câu chưa có tự dọn file | OrphanFileCleanupWorker đã có | Tách pending được dọn, failed chưa chốt retention, object ngoài metadata chưa sweep |
| Một số mô tả L10/L11 còn nói chưa có consumer | L12 đã có consumer diagnostic | Giữ mô tả theo thời điểm; thêm trạng thái hiện tại ngay đầu mục |
| Thiết kế database có nhiều bảng/cột chưa tồn tại | AppDb chưa có match_results/recommendation_items/learning_resources/content_reports/audit_events và skill projection | Giữ nhãn “thiết kế đích”; lập ma trận schema đã có/chưa có thay vì tạo mọi bảng ngay |
| Mục enum thiết kế và CHECK code có chỗ khác nhau | Ví dụ employment_type thiết kế có other, code có temporary; language/origin cũng có khác biệt theo module | Chốt hợp đồng thực dùng trước API/UI phụ thuộc, sửa docs hoặc migration có chủ đích |
| Tên báo cáo GĐ1 nhưng đã chứa tiến độ GĐ2/GĐ3 | 23 mục lịch sử dễ bị đọc nhầm là chỉ mới nền tảng | Có status summary và báo cáo theo giai đoạn từ lần tiếp theo; không xóa lịch sử |

Đây là khoảng trống truy vết tài liệu, không phải bằng chứng tất cả khác biệt đều là lỗi chạy chương trình.

## 6. Đánh giá theo các mốc lớn

| Mốc | Đánh giá | Phần cần bổ sung để đóng mốc |
|---|---|---|
| M1 — nền tảng | Nền tảng kỹ thuật có bằng chứng local/CI; chưa nghiệm thu tổng thể | Dựng lại theo README trong môi trường sạch, kế hoạch/manifest dữ liệu và ghi nhận trao đổi đánh giá; cập nhật checklist bằng SHA mới |
| M2 — tuyển dụng cơ bản | Phần lớn luồng chức năng đã có và được test | Chạy bộ acceptance đúng phiên bản, baseline trên dữ liệu có manifest/split và ghi biên bản; email thật theo dõi riêng, không chặn CRUD độc lập |
| M3 — extraction/builder | Đang thực hiện | Full CV/JD có provenance, xác nhận; template cuối được chọn; PDF tạo ra ứng tuyển được; đo extraction trước sửa |
| M4 — matching AI | Đạt phạm vi chức năng local và so sánh synthetic P10–P15 ngày 22/10; [biên bản C](../report/stage-c-2026-10-22.md) | Chất lượng đại diện/rubric người rà độc lập còn chờ; không dùng pilot synthetic làm đánh giá cuối |
| M5 — đủ chức năng | Chưa đạt | AI viết CV có duyệt, nguồn học thật, quản trị/report/usage, tích hợp provider và luồng xuyên suốt |
| M6 — bàn giao | Chưa đạt | Đánh giá cuối, dùng thử, hồi quy, restore, tài liệu/báo cáo/slide/demo |

## 7. Kế hoạch thực hiện tuần tự

Các mã P dưới đây là kế hoạch đề xuất mới, không phải task đã chạy. Giữ **L14** cho việc mở rộng extraction theo kế hoạch hiện có. Mỗi lần chỉ làm một lát UI–API–DB; chuẩn bị dữ liệu và viết báo cáo đi cùng từng lát.

### Giai đoạn A — Chốt nền hiện tại và đầu vào đánh giá

**P01. Đồng bộ hiện trạng và đóng checklist M1/M2 còn thiếu.** Cập nhật SHA/CI, trạng thái L01–L13, 15 migration và các câu cũ nêu trên. Khi Docker sẵn sàng, chạy integration PostgreSQL trên DB test riêng, E2E liên quan và thử dựng lại theo README trong thư mục/môi trường cô lập. Không reset DB ứng dụng. Đầu ra: bảng điều kiện đạt/chưa đạt có bằng chứng.

**P02. Chuẩn bị corpus và giao thức đánh giá ngay từ đầu.** Kiểm kê CV/JD được phép dùng, ID/group/hash, ngôn ngữ/định dạng/layout, nhãn trường và nhãn cặp. Tách demo, tuning, final evaluation theo nhóm tài liệu trước khi chỉnh prompt. Đề xuất quy mô theo dữ liệu/giờ thực tế, ghi rõ chưa được duyệt nếu còn chờ giảng viên. Chưa có corpus vẫn viết được parser/UI/test synthetic, nhưng chưa đóng chất lượng.

**P03. Chuẩn bị thử provider có kiểm soát.** Trước lần gọi thật: kiểm tra model/giá/kỳ, phần ngân sách tháng còn lại, secret backend và cách đối soát. Xác nhận đường extract hiện có chạy được bằng một probe nhỏ khi đã có đầu vào/quyền dùng. Không đợi hết GĐ5 mới phát hiện provider hoặc ngân sách không phù hợp. Không cần deploy để thử từ localhost.

**Điều kiện chuyển:** biết bản chuẩn đang phát triển, danh sách dữ liệu/nhãn và phần còn chờ; không còn dùng báo cáo cũ làm trạng thái hiện tại. Phần thiếu dữ liệu/provider chỉ chặn kiểm chứng phụ thuộc.

### Giai đoạn B — Hoàn tất GĐ3: đọc, xác nhận và tạo CV

**P04 / L14. Mở rộng hợp đồng trích xuất CV.** Liên hệ, summary, kinh nghiệm, dự án, học vấn, chứng chỉ và kỹ năng; trường vắng để trống; field-level provenance và source span/quote. Tách schema trích xuất với schema builder, có bộ chuyển đổi rõ ràng. Không ghi đè revision v1/v2 đã lưu.

**P05. Mở rộng parser và kiểm tra file khó.** Việt/Anh; PDF nhiều cột, bảng; DOCX body/header/footer theo phạm vi cần thiết; file lỗi và không text. Đo lỗi đọc trước lỗi LLM. Kiểm chứng cơ chế hard timeout/cô lập cho thao tác thư viện có thể chặn; không coi cancellation giữa trang là đã dừng được thư viện đồng bộ.

**P06. Nối worker → revision → màn hình rà soát đầy đủ.** Worker ghi output gốc; backend kiểm tra schema/quote; candidate sửa/xác nhận bằng revision con. Kết quả JSON sai, timeout, hết quota và restart có trạng thái riêng. Thử provider thật trên mẫu nhỏ theo P03, lưu output trước sửa và usage.

**P07. Hoàn thiện JD có cấu trúc.** Tách yêu cầu nguyên tử, required/optional, số năm khi có và quan hệ lựa chọn nếu cần; giữ câu nguồn. Recruiter rà soát. Bổ sung tập nhãn JD thay vì coi tách dòng là trích xuất hoàn chỉnh.

**P08. Hoàn thiện builder thành luồng sản phẩm.** Chọn mẫu cuối với chủ dự án; hoàn thiện lưu/mở lại/sửa và phục hồi khi upload thành công nhưng lưu revision thất bại, kể cả tải lại trang. Nối dữ liệu đã xác nhận vào form có preview. Thêm E2E **tạo PDF → ứng tuyển → recruiter tải đúng snapshot**; kiểm tra dấu, dài/ngắn, pagination và text copy. Không mặc định số mẫu đã được chốt.

**P09. Chuẩn hóa đầu vào matching và đo extraction.** Thiết kế projection kỹ năng tối thiểu có nhu cầu truy vấn, alias được kiểm tra, không gộp SQL/PostgreSQL hoặc Java/JavaScript. Quy định phiên bản/revision nào được tiêu thụ và provenance nào hợp lệ từng scope. Giải quyết việc L12 hiện bỏ qua builder schema v2; không biến user_statement thành quote file. Chạy đánh giá trước người dùng sửa, tách vi/en và PDF/DOCX.

**Đóng M3 khi:** có CV/JD đầy đủ kiểm tra được nguồn, flow tạo CV dùng để ứng tuyển, báo cáo extraction ban đầu và đầu vào đủ rõ cho rubric. Kết quả có giới hạn được ghi trung thực; không tự đặt ngưỡng đã được giảng viên duyệt.

### Giai đoạn C — Hoàn tất GĐ4: matching có điểm và giải thích

**Biên bản thực hiện 21–22/10:** [P10–P15, kiểm thử, so sánh và giới hạn](../report/stage-c-2026-10-22.md). Checklist bên dưới là tiêu chí kế hoạch gốc; kết quả thực tế được ghi trong biên bản, không coi synthetic là nghiệm thu chất lượng đại diện.

**P10. Chốt rubric thử nghiệm và ngữ cảnh phiên bản.** Xác định tiêu chí, trọng số, missing/no_evidence, required chưa có bằng chứng và điều kiện địa điểm/lương/work mode. Chốt JD snapshot hay JD hiện tại khi phân tích lại; dữ liệu đơn cũ luôn giữ nguyên. Rubric có version; chưa có góp ý giảng viên thì ghi rõ thử nghiệm.

**P11. Mở rộng job/usage đúng nhu cầu.** Hiện AiJob chủ yếu gắn CV extraction; bổ sung loại job, input snapshots/version/revision/context và method phù hợp cho match_pair, rồi các consumer. Quota user nằm trong quota tổng; dự trù số cặp/token/retry, giữ outcome_unknown, có đối soát vận hành. Làm trước khi mở lời gọi thật cho nhiều tài khoản.

**P12. Xây phân tích một cặp CV–JD trước.** LLM đối chiếu tiêu chí/bằng chứng; backend xác minh và tính điểm 0–100; lưu match_result bất biến gắn đúng input/method. UI hiển thị tiêu chí, yêu cầu bắt buộc và thiếu bằng chứng; lỗi không trở thành điểm 0. Test sai quote, score, input/version/quyền, JSON lỗi và quota.

**P13. Nối hai chiều gợi ý qua cùng bộ chấm cặp.** Dùng retrieval chọn tập có giới hạn rồi phân tích sâu; phân biệt mục chưa được chấm và mục đã chấm. Candidate dùng CV private; recruiter chỉ public hiện hành. Cache theo version/revision/method/fingerprint và kiểm tra quyền mỗi lần đọc. Giữ L09 làm baseline so sánh, không âm thầm đổi thuật toán dưới cùng tên.

**P14. Phân tích riêng tập đơn ứng tuyển.** Recruiter owner chỉ thấy hồ sơ đúng tin; dùng CV/JD snapshot hoặc ngữ cảnh phân tích lại đã chốt. Mỗi kết quả gắn application, không trộn public pool, không dùng note nội bộ trong prompt/kết quả dùng chung, không để AI đổi trạng thái.

**P15. So sánh sơ bộ và đo hiệu năng.** Baseline và phương pháp mới trên cùng tuning split; đo recall chọn tập riêng với thứ tự top-k, claim sai/không bằng chứng, độ trễ và chi phí. Hiện recruiter matching đọc và parse từng CV public theo request: đo trên dữ liệu thử rồi mới quyết định projection/index/cache cần thiết.

**Đóng M4 khi:** hai chiều + một cặp + tập đơn hoạt động đúng quyền, điểm giải thích được, cache không lộ dữ liệu sau thu hồi quyền và có kết quả so sánh ban đầu.

### Giai đoạn D — Hoàn tất GĐ5: cải thiện CV, nguồn học và quản trị

**P16. AI hỗ trợ viết CV.** Dùng snapshot dữ kiện được xác nhận; đề xuất summary/kinh nghiệm/dự án Việt–Anh; người dùng xem, sửa, chấp nhận hoặc bỏ. Chỉ sau chấp nhận mới tạo nội dung/version mới. Test thêm dữ kiện không có nguồn và hủy đề xuất.

**P17. Tìm nguồn học thật.** Xây search adapter và flow người dùng yêu cầu theo khoảng thiếu; trả tên/nguồn/URL/ngôn ngữ/lý do; chỉ dùng URL từ search, giới hạn query/credit, điều khoản cache/TTL và không PII. Không có kết quả/hết credit vẫn xem được match_result. Fake hiện trả rỗng không đáp ứng yêu cầu này.

**P18. Quản trị, báo cáo nội dung và usage.** Admin quản lý tài khoản/tin, nhận/xử lý báo cáo, audit tối thiểu và theo dõi lỗi/usage/budget. Không thêm quyền mặc định đọc CV private. Hoàn thiện email adapter và kiểm chứng reset thật tới địa chỉ chủ dự án kiểm soát; theo dõi độc lập với deploy.

**P19. Hoàn thiện UX và ba luồng sản phẩm.** Có điều hướng Candidate/Recruiter/Admin rõ, không yêu cầu người dùng nhập ID kỹ thuật để làm tác vụ thường gặp; thay key/JSON thô bằng thông tin dễ hiểu. Hoàn thiện loading/lỗi/quota/mobile cơ bản. Kiểm tra xuyên suốt cả ba luồng của roadmap; không đổi stack chỉ vì tài liệu có đề xuất thư viện chưa dùng.

**Đóng M5 khi:** đủ chức năng cốt lõi; DeepSeek/search/email cần thiết được kiểm chứng thật trong hạn mức; không có lỗi quyền/snapshot chặn sản phẩm; chốt method và final evaluation split.

### Giai đoạn E — GĐ6: đánh giá và bàn giao localhost

**P20. Đánh giá cuối bằng phiên bản cố định.** Chạy baseline và phương pháp AI cùng tập cuối chưa dùng tinh chỉnh; đo extraction trước sửa, hai chiều retrieval/ranking, điểm/bằng chứng, nguồn học, độ trễ và usage. Ghi coverage nhãn và giới hạn. Chủ dự án tổ chức dùng thử với giảng viên; không ghi lịch hẹn thành kết quả dùng thử.

**P21. Ổn định, khôi phục và bàn giao.** Sửa lỗi, regression đúng commit; backup/restore DB+objects+DP keys, replay ledger, kiểm tra account/file/job sau restore cô lập. Chốt retention file failed và cách nhắc/chạy expiry local theo chính sách; chưa có scheduler thì nói rõ phạm vi vận hành. Thử README trên môi trường sạch, hoàn thiện báo cáo, slide và demo hai vai trò/luồng cải thiện CV, kịch bản API lỗi và video dự phòng có nhãn.

**Đóng M6 khi:** chức năng, thực nghiệm và hướng dẫn bàn giao có bằng chứng ở cùng phiên bản; không còn lỗi chặn ba luồng. Deploy chỉ là bước sau nếu chủ dự án yêu cầu mở lại.

## 8. Lịch điều hành đề xuất

Đây là **thứ tự và ngân sách thời gian quản lý**, không phải cam kết ngày hoàn thành. Chưa biết ngày bắt đầu thực tế, hạn nộp và số giờ/tuần nên không thể kết luận “còn đúng N tuần” hoặc dự án đang ở tuần mấy.

| Đợt từ hiện tại | Trọng tâm đề xuất | Kết quả cần có |
|---|---|---|
| Đợt 1 | P01–P03; bắt đầu L14 | Baseline đúng commit, corpus/protocol, chuẩn bị provider |
| Đợt 2 | P04–P09 | Đóng các điều kiện GĐ3/M3 còn thiếu |
| Đợt 3 | P10–P12 | Chấm được một cặp có rubric, quota và bằng chứng |
| Đợt 4 | P13–P15 | Hai chiều, tập đơn, cache và so sánh sơ bộ |
| Đợt 5 | P16–P19 | Đủ chức năng cải thiện CV/nguồn học/quản trị; chốt M5 |
| Hai tuần cuối của lịch đã thống nhất | P20–P21 | Đánh giá, sửa lỗi, restore và bảo vệ |

Sau 2–3 lát tiếp theo, dùng số giờ thực làm để ước lượng lại: tổng giờ việc còn lại / giờ hữu dụng mỗi tuần, cộng thời gian sửa lỗi và chờ đầu vào. Không ép các đợt vào số tuần cũ nếu chưa đủ công sức. Giảm trang trí, bộ lọc phụ hoặc mẫu CV thứ ba trước; giữ hai chiều matching, giải thích, tạo CV, nguồn học và thực nghiệm.

## 9. Cách dùng Codex full access cho kế hoạch này

- Giao mỗi lượt một lát cụ thể có đầu ra chạy được, thay vì “làm hết GĐ3–GĐ5”. Codex đọc AGENTS/tài liệu và Git, tự xử lý lựa chọn kỹ thuật nhỏ, sửa code/tài liệu liên quan, chạy test theo rủi ro rồi ghi bằng chứng.
- Full access giúp đọc/sửa/build/test local tự chủ; không tạo được dữ liệu đánh giá đáng tin, sự đồng ý dùng CV, phản hồi giảng viên hoặc ngân sách/provider còn thiếu.
- Chủ dự án tập trung vào: dữ liệu hợp lệ, góp ý rubric/lịch đánh giá, mẫu CV cuối, JD khi phân tích lại và dịch vụ/ngân sách. Không hỏi lại các quyền/role/owner/90–7–14 ngày đã chốt.
- Giữ quy tắc repository hiện có: không tự push và không mở lại deploy. Các phần local độc lập vẫn tiến hành khi chờ đầu vào; không dùng việc chưa có hosting để dừng phát triển.
- Mỗi lát ghi: phạm vi → diff/commit → kiểm tra đã chạy → kết quả → giới hạn → việc kế tiếp. Sau khi chủ dự án publish, đối chiếu CI đúng SHA. Không chạy lại mọi test/API trả phí cho sửa tài liệu nhỏ.

**Việc nên bắt đầu ngay:** đồng bộ trạng thái b392ef0/CI, chốt checklist nghiệm thu và dữ liệu, rồi thực hiện **L14 — extraction CV đầy đủ có provenance và rà soát**, đồng thời chuẩn bị probe DeepSeek có giới hạn. Không xây thêm CRUD tuyển dụng đã có hoặc chuyển sang deploy.

## 10. Nguồn đối chiếu chính trong repository

- `D:\CareerGapAI\AGENTS.md`, `README.md`.
- `project-docs/01-system-specification.md`, `02-tech-stack-decisions.md`, `03-roadmap.md`, `04-database-design.md`.
- `project-docs/tasks/next-local-development-plan.md`, `tasks/phase-01-foundation.md`.
- `project-docs/report/phase-01-foundation-report.md`, đặc biệt mục 17–23.
- `project-docs/phase-01-implementation.md`, `phase-01-data-retention.md`, `phase-01-services-checklist.md`, `evaluation/phase-01-evaluation.md`, `dependency-licenses.md`.
- `backend/CareerGap.Api/Data/Models.cs`, `AppDb.cs`, `Data/Migrations/`; các module Accounts, Profiles, Recruitment, Files, Applications, Ai, Matching và Integrations.
- `frontend/src/`, `frontend/e2e/`, `tests/CareerGap.Api.Tests/`, `scripts/`, `.github/workflows/ci.yml`.
- TRX lịch sử trong `tests/CareerGap.Api.Tests/TestResults/`; manifest `.local/evaluation/matching-l01-l09-20261016.json`.
- Kiểm tra offline mới: `D:\Codex\2026-10-16\ha\work\test-results\audit-offline.trx`.

Lượt này tạo báo cáo và chạy kiểm tra; không sửa mã nguồn/tài liệu gốc của CareerGapAI, không áp migration, không reset DB, không deploy hoặc gọi provider trả phí.
