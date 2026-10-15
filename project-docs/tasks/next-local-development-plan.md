# Kế hoạch công việc tiếp theo — hoàn thiện nghiệp vụ trên localhost

Ngày lập: 15/10/2026. Trạng thái: **kế hoạch đề xuất, chưa thực thi**. Một người thực hiện, một lát UI–API–DB tại mỗi thời điểm. Không commit/push, deploy hoặc gọi provider thật trong lượt lập kế hoạch này.

## 1. Kết luận và căn cứ

**Ưu tiên tiếp theo: xây luồng recruiter đăng tin → candidate ứng tuyển bằng CV riêng → recruiter đọc đúng snapshot và sàng lọc → candidate thấy kết quả trên localhost.** Kiểm tra lại nền tảng đang có trước khi thêm chức năng; không làm lại nền tảng từ đầu và không chờ dịch vụ cloud.

Đã đọc 11 tệp hiện có trong `project-docs/`, đối chiếu Git working tree, README, cấu hình test/CI, Accounts, models và các endpoint/file/test nền tảng. Đây là khảo sát để lập kế hoạch, không phải audit toàn bộ code hoặc nghiệm thu runtime.

| Hiện trạng | Bằng chứng và giới hạn |
|---|---|
| Có prototype self/private | Code có login/logout/CSRF/session, upload tạo CV/version 1, tải file chính chủ, extract probe, worker và ngân sách fake. Chưa có registration/profile hoàn chỉnh, công ty/JD, ứng tuyển hoặc sàng lọc |
| Có kiểm thử nền tảng | [Report GĐ1](../report/phase-01-foundation-report.md) ghi 19 backend, 3 frontend và E2E thành công ở đợt trước. Lượt lập kế hoạch không chạy lại, không coi số này là chứng nhận cho working tree hiện tại |
| M1 chưa nghiệm thu đầy đủ | Theo [roadmap](../03-roadmap.md), còn kiểm tra dựng lại, đối chiếu điều kiện local và đầu vào dữ liệu. Thiếu cloud/AI thật không còn là điều kiện chặn M1 local |
| Working tree đang có thay đổi | HEAD đọc được là `bdb3aeb`; có sửa code/test/tài liệu và chuyển các tệp `docs/` sang `project-docs/`. Giữ các thay đổi này, review trước khi sửa tiếp |
| Liên kết tài liệu chưa theo kịp việc chuyển thư mục | Ví dụ README còn `docs/tasks/phase-01-foundation.md`, tài liệu 01 còn `../docs/phase-01-data-retention.md`, trong khi `docs/` không còn. Sửa đường dẫn trong bước L01; giữ nguyên các mục lịch sử có nhãn |
| Tích hợp thật chưa đủ bằng chứng | DeepSeek/search/email còn kiểm chứng riêng. Checklist ghi Neon đã cấu hình nhưng đang dừng thao tác cloud; không suy ra chưa có dịch vụ hoặc cần tạo lại |

Nguồn nghiệp vụ: [01](../01-system-specification.md), kỹ thuật: [02](../02-tech-stack-decisions.md), phụ thuộc/nghiệm thu: [03](../03-roadmap.md), thiết kế đích: [04](../04-database-design.md). Schema 04 là đề xuất; chỉ triển khai phần cần cho từng lát, giữ các chính sách đã chốt.

## 2. Thứ tự thực hiện và điều kiện đóng việc

Mỗi hàng gồm cả giao diện, API, migration nếu cần và kiểm thử liên quan. Không tạo toàn bộ schema tương lai trong một đợt.

| ID / phụ thuộc | Công việc cụ thể | Điều kiện đóng / kiểm tra bắt buộc |
|---|---|---|
| **L01 — bắt đầu ngay** | Review diff đang có; sửa liên kết do chuyển thư mục trong README/AGENTS/tài liệu; dựng lại theo README với cấu hình local và dữ liệu giả tách biệt. Đối chiếu 6 điều kiện M1 bằng bảng Đạt/Chưa đạt/Chưa kiểm chứng | Ghi lệnh, phiên bản/diff và kết quả mới; frontend/backend, migration PostgreSQL 16 và E2E login→private file→fake job chạy được. Không reset DB ứng dụng. CI cũ chỉ gắn `bdb3aeb`; CI commit mới chờ chủ dự án publish, không chặn lập trình local độc lập |
| **L02 — sau L01; hồ sơ cá nhân** | Đăng ký một role Candidate/Recruiter; đổi/khôi phục mật khẩu qua fake email; profile chính chủ và allowlist trường chia sẻ. Bổ sung concurrency token cho mutation; màn hình điều hướng theo vai trò | Không tự cấp Admin/thêm role; email trùng, password không hợp lệ, CSRF, reset token sai/hết hạn/dùng lại được kiểm tra. Không log token. Candidate khác không đọc profile; recruiter chỉ nhận trường chia sẻ từ DTO. Fake chỉ nghiệm thu logic reset local |
| **L03 — sau L02; JD công khai** | Công ty một recruiter owner; tạo/sửa JD thành version bất biến; draft/publish/close; tìm, lọc, phân trang và lưu việc. Dựng form/bảng dùng được trước khi trang trí | Recruiter B không sửa tin/công ty A; không đổi owner qua client. Khách đọc được JD công khai, không tin nháp. Tin đóng/hết hạn không nhận đơn; deadline kiểm tra theo thời gian server. Sửa JD tạo version mới, không sửa version cũ |
| **L04 — sau L02; CV có phiên bản** | Đặt tên/quản lý CV, thêm file version vào CV đã có; giữ upload private; UI chọn đúng CV/version để chuẩn bị ứng tuyển | Khóa hàng cha khi cấp số; hai upload đồng thời không trùng số hoặc ghi đè file. FK owner/version và file ready/hash hợp lệ. File của candidate A vẫn bị chặn với B/recruiter/admin khi chưa có ngữ cảnh cấp quyền |
| **L05 — sau L03 + L04; ứng tuyển/snapshot** | Gửi CV vào tin đang tuyển, ghim CV version và JD version theo thiết kế đề xuất; revision nullable; tạo đơn/history/thông báo trong transaction. Danh sách đơn hai phía và đường đọc file theo application | Ứng tuyển hoạt động khi AI tắt/hết quota. Hai request chỉ tạo một đơn hiệu lực; cùng key/hash trả đơn cũ, khác hash trả conflict. Apply cạnh tranh close/edit JD có thứ tự nhất quán. CV/JD đổi sau nộp không đổi snapshot; recruiter tin khác không đọc được qua ID trực tiếp. Ghim JD là lựa chọn kỹ thuật đề xuất cần ghi rõ trong hợp đồng trước triển khai, không phải quyết định đã xác nhận |
| **L06 — sau L05 và quyết định chuyển trạng thái** | Sàng lọc thủ công, ghi chú nội bộ, rút đơn, block/bỏ block theo tin, nộp lại bằng đơn mới; thông báo trong hệ thống. Thực thi hạn đọc 90 ngày ngay trong truy vấn quyền | Application/history/notification atomic; stale row_version bị conflict. Ghi chú không xuất hiện ở DTO/thông báo candidate. Block tin A không ảnh hưởng B; block/apply cạnh tranh được kiểm tra. Rút/rejected không mở lại đơn cũ; nộp lại không kéo dài hạn đơn trước; tại mốc 90 ngày đường đọc cũ bị chặn dù chưa dọn file |
| **L07 — sau L04; CV công khai và quyền đọc** | Một lựa chọn public ghim version; publish/hide/replace có concurrency. Recruiter đọc CV có liên hệ; candidate khác chỉ bản dẫn xuất an toàn dựng từ dữ liệu cho phép, không sửa file gốc | Hai publish chỉ một lựa chọn; upload mới không tự public. Ẩn/thay chặn link public cũ. Bản dẫn xuất phải đúng source hash và không còn liên hệ trong text/link/metadata/QR; chưa xác minh thì trả chưa sẵn sàng, không fallback gốc. Có bài kiểm tra lộ liên hệ. Chưa xong bản xem an toàn thì ghi hạng mục chưa hoàn tất, không nhận đủ tính năng public CV |
| **L08 — sau L05–L07; lưu/xóa** | Thiết kế migration tiến cho deletion request/purge ledger, thu hồi quyền ngay, hủy trong 7 ngày, purge/ẩn danh và dọn object có retry/idempotency. Đối soát file dùng chung và dữ liệu usage còn chờ settlement | Kiểm tra bằng thời gian điều khiển được: T0 khóa file/job/result/link; hủy không tự public lại CV; đến 7 ngày thực thi purge. Không cascade dữ liệu người khác hoặc xóa file còn tham chiếu hợp lệ. Restore local cô lập áp lại ledger trước mở quyền; backup chứa dữ liệu purge phải hết hạn trong 14 ngày tiếp. Không mở API xóa cho sử dụng trước khi chứng minh được cơ chế thực thi và lỗi dọn |
| **L09 — sau dữ liệu CV/JD + ngữ cảnh public/application** | Baseline từ khóa hai chiều, cấu hình và script chạy lại; lưu retrieval score riêng. Kiểm kê/gán nhãn dữ liệu xuyên suốt, không chờ L09 mới bắt đầu chuẩn bị | CV→JD chỉ tin còn tuyển; JD→CV chỉ public pool đủ quyền, không lẫn CV nộp riêng. Cùng dữ liệu/cấu hình tái tạo được kết quả; ghi rõ demo giả, tuning và evaluation. Không gọi baseline là điểm AI 0–100 hoặc coi cặp chưa gán nhãn là negative |

L03 và L04 có thể đổi thứ tự; một người hoàn thành từng lát. L07 không phải điều kiện để nộp CV riêng ở L05. L08 không cần chờ matching nhưng phải hoàn thành trước khi mở chức năng xóa tài khoản. L09 không cần chờ rubric điểm LLM cuối cùng; baseline vẫn phải có cấu hình và dữ liệu truy vết.

**Đợt triển khai đầu tiên chỉ nhận L01–L03.** Đầu ra mong đợi: candidate/recruiter đăng ký, quản lý profile/công ty theo quyền; recruiter đăng JD và khách/candidate tìm thấy JD công khai. Sau đó mới nhận L04–L06 để nối ứng tuyển/sàng lọc. Đây là giới hạn việc đang làm, không cắt L07–L09 khỏi M2 hoặc phần lưu/xóa đã chốt.

## 3. Khoảng trống nền tảng xử lý đúng thời điểm

- Parser: đo thêm file khó Việt–Anh, nhiều cột/bảng và DOCX header/footer trước khi cam kết độ phủ trích xuất ở GĐ3. Thử giới hạn thực; bổ sung cô lập/hard timeout nếu thao tác thư viện có thể treo ngoài cancellation. Không dùng `Task.WhenAny` để tuyên bố đã dừng được parser đồng bộ. Chưa cần biến mọi cải tiến parser thành điều kiện chặn form công ty/JD.
- File pending/mồ côi: thiết kế dọn có khoảng đệm và kiểm tra tham chiếu, thực hiện cùng L08; không xóa hàng loạt để làm sạch DB hoặc xóa file snapshot đang dùng.
- Worker/quota: giữ regression hiện có cho lease, restart, unknown cost và settlement. Mở rộng theo nhu cầu job GĐ3–5; user quota và đối soát provider phải hoàn thiện trước khi mở sử dụng thật cho nhiều tài khoản. Chưa cần viết lại queue để bắt đầu CRUD nghiệp vụ.
- Backup local: kiểm chứng DB + objects + DP keys trên môi trường cô lập trước mutation/purge có rủi ro mất dữ liệu; không dùng việc chỉ restore DB để kết luận đã khôi phục đủ.

## 4. Quyết định còn thiếu — chỉ chặn đúng phần phụ thuộc

| Quyết định | Chuẩn bị được ngay | Mốc cần xác nhận |
|---|---|---|
| Chuyển trạng thái sàng lọc chi tiết | Soạn bảng actor/from/to; phân biệt trạng thái hiệu lực/kết thúc và cách trả conflict. Luồng đề xuất trong 04 chưa phải chính sách đã chốt | Trước mutation đổi trạng thái L06; không cản L01–L05 |
| JD cũ hay mới khi phân tích lại | Giữ snapshot bất biến, lưu ID phiên bản và hiển thị phiên bản đã nộp; tách thiết kế request phân tích lại | Trước API AI phụ thuộc; không dùng quyết định này để chặn ứng tuyển không AI |
| Số lượng/thiết kế mẫu CV | Chuẩn bị mẫu xem trước; kế hoạch cơ sở 2 mẫu vẫn là đề xuất | Trước xây mẫu sản phẩm GĐ3 |
| Rubric, quy mô dữ liệu, top-k và ngưỡng | Chuẩn bị manifest, hướng dẫn nhãn và đề xuất giao thức thử; người thực hiện trao đổi giảng viên | Trước thực nghiệm phụ thuộc; chưa phản hồi thì ghi thử nghiệm, không giả đã duyệt |
| Dịch vụ thật/ngân sách còn lại | Tiếp tục adapter/fake và test lỗi; dùng checklist hiện có để ghi đầu vào khi được cung cấp | Trước gọi DeepSeek/search/email thật; không yêu cầu hosting để tiếp tục local |

Không hỏi lại UI tiếng Việt, một role khi đăng ký, owner công ty, quyền khách/admin/CV ẩn liên hệ, block theo tin hoặc 90/7/14 ngày. Không gửi liên hệ giảng viên hoặc bên ngoài thay người dùng khi chưa được yêu cầu.

## 5. Sau đợt nghiệp vụ

1. **GĐ3:** revisions CV/JD, provenance và xác nhận; trích xuất Việt–Anh; bộ tạo CV lưu/mở lại/xuất PDF dùng được để ứng tuyển. Đánh giá output trước sửa. Extract probe và PDF probe hiện tại không thay đầu ra này.
2. **GĐ4:** chọn tập rồi chấm sâu có giới hạn, matching hai chiều và phân tích tập đơn riêng; điểm/bằng chứng nhất quán, cache theo version/method và quyền hiện tại. So sánh baseline trên cùng dữ liệu.
3. **GĐ5:** hoàn thiện AI viết CV có người duyệt, nguồn học URL thật, quản trị/báo cáo và usage; kiểm chứng provider thật từ localhost trong ngân sách đã được phép trước khi nhận đủ chức năng. Không đẩy tạo CV/nguồn học thành phần mở rộng.
4. **GĐ6:** đánh giá cuối với phương pháp cố định, sửa lỗi, restore và bàn giao localhost. Chỉ mở lại deploy khi sản phẩm hoàn thiện và người dùng yêu cầu rõ ràng.

Không cam kết số ngày hoàn thành khi chưa có số giờ làm thực tế. Mục tiêu 12 tuần cần tính lại sau L01–L03 dựa trên công sức đo được; đây không phải bằng chứng lịch còn khả thi. Giữ thời gian đánh giá cuối; giảm trang trí/bộ lọc phụ trước khi cân nhắc thay đổi phạm vi cốt lõi.

## 6. Cách kiểm tra và cập nhật tiến độ

- Từ root `D:/CareerGapAI`: theo README, nạp môi trường local bằng `. ./scripts/dev-env.ps1`, khởi động `docker compose up -d db`, restore tool/package. Trước migration/test phải xác nhận đích là PostgreSQL local, không dùng cấu hình Neon. Không in nội dung `.env` hoặc secret.
- `./scripts/test.ps1`: backend integration trên database ngẫu nhiên `careergap_test_*`, frontend lint/typecheck/unit/build. Khi đổi schema, thêm ca DB mới và nâng cấp từ bản trước có dữ liệu giả; đọc Up/Down/SQL.
- Từ `frontend/`: `npm run e2e` cần API/Vite hoặc `CAREERGAP_BASE_URL`, Chromium, tài khoản và fixture giả. Thêm kịch bản đúng lát: hai candidate/hai recruiter, request ID trực tiếp, snapshot và concurrency; không chỉ happy path.
- `docker build -t careergap:phase01 .` và smoke theo README khi thay đổi cần kiểm tra Linux/cùng origin. Không lặp full Docker/API thật vì chỉ sửa liên kết Markdown.
- Mỗi việc đóng phải có diff, lệnh/kết quả, môi trường và lỗi còn lại; cập nhật hợp đồng triển khai/tài liệu schema liên quan. M2 chỉ đạt khi đủ điều kiện roadmap, không phải chỉ xong L03 hoặc hết tuần. Bằng chứng CI mới chỉ bổ sung sau khi chủ dự án publish và run đúng commit thành công.

Kiểm tra trong lượt lập kế hoạch: đọc tài liệu, khảo sát code/cấu hình và Git; kiểm tra liên kết của tệp kế hoạch và whitespace. **Chưa chạy build, migration, test sản phẩm hoặc API thật trong lượt này.**
