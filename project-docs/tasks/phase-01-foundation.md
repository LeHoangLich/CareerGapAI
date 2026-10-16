# GĐ1 — Nền tảng, quyền và đánh giá

> **Quyết định mới ngày 15/10/2026:** dừng deploy, ưu tiên hoàn thiện localhost. Kế hoạch và nhật ký cũ bên dưới được giữ để truy vết; các yêu cầu cloud/AI thật trên demo và danh sách G1-R01–R08 cũ không còn là thứ tự thực hiện hiện tại. Xem mục cuối **Ưu tiên hiện tại — localhost** và điều kiện M1 mới trong roadmap. Chưa tự đánh dấu M1 đạt.

Ngày lập: 15/10/2026. Trạng thái ban đầu: **Chưa bắt đầu**. Người dùng đã xác nhận **tự thực hiện một mình**. Lịch mục tiêu: tuần 1–2 tính từ ngày người thực hiện khởi động, chưa gán ngày thực tế; không mặc định có nhiều người để chia việc.

## Mục tiêu và nguồn yêu cầu

Đạt M1 localhost bằng luồng đăng nhập → tải/đọc file private đúng quyền → job fake có hạn mức test_credit; kiểm chứng nền tảng local và chuẩn bị dữ liệu đánh giá. Một lần tích hợp thành công không chứng minh chất lượng AI hoặc hoàn thành nghiệp vụ GĐ2–5.

Nguồn: [đặc tả](../../project-docs/01-system-specification.md), [công nghệ](../../project-docs/02-tech-stack-decisions.md), [roadmap — GĐ1/M1](../../project-docs/03-roadmap.md), [database](../../project-docs/04-database-design.md), [hướng dẫn agent](../../AGENTS.md).

Hiện trạng đầu đợt: chỉ có bốn tài liệu và AGENTS.md; chưa có Git, mã nguồn, migrations hay build/test. Không mặc định đã có tài khoản provider, cloud, domain, sender hoặc dữ liệu thật được phép sử dụng.

## Kế hoạch gốc — lưu lịch sử trước quyết định dừng deploy

Trạng thái: Chưa bắt đầu / Đang làm / Bị chặn / Hoàn thành. **Chủ dự án** là người phụ trách duy nhất cho backend/hạ tầng, frontend/tích hợp và dữ liệu/kiểm thử; agent hỗ trợ trong từng yêu cầu, không phải một nhân sự được tính vào tiến độ. Không còn phân công A/B/C. Chưa xác nhận số giờ làm mỗi tuần.

| ID | Người phụ trách | Phụ thuộc | Trạng thái ban đầu |
|---|---|---|---|
| G1-01 | Chủ dự án | Không | Chưa bắt đầu |
| G1-02 | Chủ dự án | G1-01 | Chưa bắt đầu |
| G1-03 | Chủ dự án | G1-01 | Chưa bắt đầu |
| G1-04 | Chủ dự án | G1-02, G1-03; phần self độc lập | Chưa bắt đầu |
| G1-05 | Chủ dự án | G1-04 | Chưa bắt đầu |
| G1-06 | Chủ dự án | G1-04, G1-05 | Chưa bắt đầu |
| G1-07 | Chủ dự án | G1-04, G1-06 | Chưa bắt đầu |
| G1-08 | Chủ dự án | G1-05–07 | Chưa bắt đầu |
| G1-09 | Chủ dự án | Bắt đầu G1-03; nghiệm thu sau G1-08 | Chưa bắt đầu |
| G1-10 | Chủ dự án | G1-03 | Chưa bắt đầu |
| G1-11 | Chủ dự án | G1-01 | Chưa bắt đầu |
| G1-12 | Chủ dự án | Các kết quả trên | Chưa bắt đầu |

### G1-01 — Kiểm kê

- [x] Ghi công cụ/phiên bản thực tế, trạng thái Docker/PostgreSQL và lệnh đã chạy.
- [x] Chủ dự án là người quản lý repository, cloud, DNS/sender và ngân sách; không đọc/in secrets. Việc đã có tài khoản/quyền từng dịch vụ vẫn phải kiểm kê riêng.
- [ ] Xác nhận nguồn CV/JD, quyền dùng, lịch và số giờ người thực hiện có thể dành mỗi tuần.
- Đầu ra/nghiệm thu: danh sách có bằng chứng; mọi thông tin chưa cung cấp ghi chưa xác nhận.

### G1-02 — Quyết định và quyền

- [x] Ghi ma trận quyền và danh sách quyết định còn mở: liên hệ CV công khai, khách/admin, UI, vai trò tài khoản, owner công ty, trạng thái, rút/nộp lại và lưu/xóa dữ liệu.
- [x] Cập nhật quyết định người dùng xác nhận vào đặc tả/thiết kế dữ liệu: UI/khách/admin/role/owner, CV ẩn liên hệ, đọc sau rút, nộp lại và block theo tin, lưu/xóa 90/7/14 ngày. API/migrations tương ứng vẫn thuộc phần triển khai kế tiếp.
- Đầu ra/nghiệm thu: self-only không mở quyền công khai; phần chưa chốt không được diễn giải thành chính sách đã duyệt.

### G1-03 — Nền tảng

- [x] Khởi tạo Git local, một Web API .NET 10, một project test và React/TS/Vite.
- [x] Khóa phiên bản, lockfile, kiểm tra giấy phép dependency đúng phiên bản.
- [ ] PostgreSQL local cùng major CI/demo; cấu hình mẫu, ignore secrets và README lệnh thực tế. Local/CI đã kiểm chứng; demo cloud chưa có.
- Đầu ra/nghiệm thu: restore/build/test theo README trong môi trường sạch và ghi bằng chứng; không còn yêu cầu ba thành viên chạy thử. Phần deploy cloud theo G1-09.

### G1-04 — API và schema

- [x] REST /api, OpenAPI, DTO và ProblemDetails.
- [x] Migrations theo lát: Identity/DP, candidate profile tối thiểu, file/CV versions, method/jobs/budgets/attempts.
- [x] FK owner/version, numeric tiền, thời gian UTC, bất biến version và migration triển khai riêng.
- Đầu ra/nghiệm thu: PostgreSQL thật kiểm tra database mới và nâng cấp có dữ liệu; không tạo schema tương lai chỉ để đủ bảng.

### G1-05 — Tài khoản

- [x] Identity/cookie, phiên hiện tại, CSRF, login/logout, kiểm tra account status/owner.
- [x] Tài khoản giả tạo bằng lệnh kiểm soát, không mật khẩu cố định; không API tự cấp Admin.
- [ ] DP key bền vững, production bảo vệ bằng chứng thư và hướng dẫn backup khóa.
- Đầu ra/nghiệm thu: sai mật khẩu/CSRF/sai quyền bị chặn; kiểm tra qua proxy/redeploy riêng.

### G1-06 — File

- [x] Upload private → xác minh → ready → CV version, không giữ transaction lúc upload.
- [ ] Giới hạn file/trang/text/giải nén/thời gian; không tải external resources hoặc chạy macro.
- [ ] Parser PDF/DOCX giữ nguồn trang/đoạn, thử Việt–Anh, nhiều cột/bảng, scan và file lỗi. Đã thử bộ giả và PDF không text; chưa có scan thật/bộ bố cục đa dạng.
- [ ] Đọc theo ID qua backend kiểm tra chính chủ; object bất biến và xử lý upload dở.
- Đầu ra/nghiệm thu: hai ứng viên không đọc chéo; recruiter/admin không được mặc định xem private; file scan không có dữ liệu giả.

### G1-07 — Job và ngân sách

- [x] Fake LLM/search/email mặc định; typed interfaces cho provider.
- [x] PostgreSQL queue/lease/generation; idempotency key/hash; request trả 202, khác hash trả 409.
- [x] Giữ ngân sách atomic, attempts/allocations, đối soát một lần; timeout giữ khoản chưa rõ; retry tối đa một lần trong dự trù.
- [x] Validate schema và nguồn trước phát hành, kiểm tra quyền lúc gửi/trả kết quả; không chấm điểm giả.
- [ ] Thử DeepSeek thật có chủ đích sau khi có cấu hình/model/giá và ngân sách đã kiểm tra.
- Đầu ra/nghiệm thu: restart không mất job, worker mất lease không publish, usage thực được ghi; fake không được gọi là AI thật.

### G1-08 — Giao diện

- [x] Đăng nhập, upload/mở CV riêng, tạo job, polling và đọc kết quả.
- [x] Thể hiện loading/lỗi/quota và nhãn fake rõ; không lộ stack trace/lease/token.
- Đầu ra/nghiệm thu: luồng browser xuyên suốt; UI tiếng Việt đã được người dùng xác nhận, CV/JD Việt–Anh; không suy ra UI song ngữ.

### G1-09 — CI và demo

- [x] CI frontend lint/typecheck/build, backend test với PostgreSQL thật và Docker build; dùng fake. Run 38718678231 success cho bdb3aeb; thay đổi local sau đó chưa có CI mới.
- [ ] Kiểm tra proxy, cookie/CSRF/multipart, DP và file sau restart/redeploy.
- [ ] Ghi URL/version/migration thật; nếu Vercel không phù hợp kiểm chứng React static trên .NET.
- Đầu ra/nghiệm thu: người thực hiện dựng lại được theo README, CI đúng commit pass và cloud có một luồng AI thật; chưa có tài khoản thì ghi bị chặn, không mua/nâng gói.

### G1-10 — Các thử nghiệm sớm

- [x] Xuất PDF có font Việt, xuống dòng/phân trang và chọn/copy text.
- [ ] Search một kỹ năng không có PII, URL từ provider; kiểm tra credits/điều khoản cache.
- [ ] Kiểm tra quyền DNS/sender; fake email không chứng minh gửi thật.
- Đầu ra/nghiệm thu: bằng chứng hoặc phương án xử lý cụ thể cho từng thử nghiệm; không tự mua domain.

### G1-11 — Dữ liệu và đánh giá

- [x] Manifest nguồn, giấy phép/đồng ý, ngôn ngữ, định dạng, nhóm tài liệu và mục đích. Hiện chỉ bộ giả; chưa có CV/JD thật hợp lệ.
- [x] Schema nhãn bằng chứng, chia nhóm train-tuning/evaluation tránh leakage; demo tách đánh giá. Đây là kế hoạch, chưa chia tập thật.
- [x] Kế hoạch đánh giá trích xuất trước sửa, chọn tập/xếp hạng hai chiều và giải thích, baseline cùng dữ liệu.
- [x] Danh sách cần giảng viên góp ý đã ghi trong kế hoạch đánh giá; chưa có bằng chứng đã trao đổi, không tự đặt ngưỡng hoặc ghi đã được duyệt.
- Đầu ra/nghiệm thu: dữ liệu giả được đánh dấu, không đại diện chất lượng AI; chưa có CV thật thì ghi rõ.

### G1-12 — Nghiệm thu/report

- [x] Cập nhật kết quả từng task, lệnh/thư mục/kết quả và bằng chứng.
- [x] Đối chiếu sáu điều kiện M1 trong roadmap theo Đạt / Chưa đạt / Chưa kiểm chứng.
- [x] Sau đợt thực thi tạo [report](../report/phase-01-foundation-report.md), có chi phí/usage, lỗi, phụ thuộc và bước tiếp theo.
- Đầu ra/nghiệm thu: report trung thực; thiếu bất kỳ điều kiện M1 thì kết luận Chưa đạt M1.

## Nhịp và giới hạn

Tuần 1: quyết định, nền tảng, API/schema, luồng local fake và dữ liệu. Tuần 2: cloud, thử thật có giới hạn, restart, PDF/search/email và nghiệm thu. Hết tuần không tự hoàn thành.

Ngân sách khoảng 4 USD/tháng cho toàn bộ môi trường/thực nghiệm/demo của một người; tách USD và search credit; không bật pay-as-you-go. Chỉ self/private trong đợt này; không mở API ứng tuyển/công khai CV hoặc tự chốt quyền còn mở. Không cắt tạo CV, nguồn học hay matching hai chiều khỏi các giai đoạn sau.

## Nhật ký thực thi

Ngày cập nhật 15/10/2026. Bảng ban đầu giữ làm mốc kế hoạch; trạng thái thực tế sau đợt local:

| Task | Trạng thái hiện tại | Kết quả/phụ thuộc còn lại |
|---|---|---|
| G1-01 | Đang làm | Công cụ/GitHub/CI đã kiểm kê, chủ dự án là người duy nhất phụ trách; còn trạng thái tài khoản provider, dữ liệu và giờ làm cá nhân |
| G1-02 | Hoàn thành | Các quyết định quyền/role/owner/UI/nộp lại-block theo tin/lưu-xóa ưu tiên đã được người dùng xác nhận và cập nhật; các chuyển trạng thái chi tiết và API thực thi hoàn thiện trước luồng GĐ2 tương ứng |
| G1-03 | Đang làm | Nền tảng local/lockfile/README và remote sẵn sàng; còn kiểm tra dựng lại môi trường theo README, cloud theo G1-09; bỏ điều kiện nhiều người |
| G1-04 | Hoàn thành | Hợp đồng self/private và lát schema tối thiểu, đã thử PostgreSQL mới/nâng cấp/Down; giới hạn so với thiết kế đích đã ghi rõ |
| G1-05 | Đang làm | Cookie/CSRF/owner qua local proxy và restart host đã thử; chưa có HTTPS/DP cert trên cloud |
| G1-06 | Đang làm | File private/parser bộ giả chạy; còn S3 thật, hard timeout và bộ file khó/scan thật |
| G1-07 | Đang làm | Queue/lease/idempotency/quota/timeout có test fake; còn DeepSeek thật và đối soát provider |
| G1-08 | Hoàn thành | UI prototype local, fake label rõ; browser desktop/mobile và PDF đã kiểm tra |
| G1-09 | Đang làm | CI GitHub bdb3aeb success, Docker build/E2E local đã qua; còn public deploy và kiểm chứng cloud |
| G1-10 | Đang làm | PDF Việt đã thử; search/email/DNS/sender cần tài khoản và kiểm chứng thật |
| G1-11 | Đang làm | Manifest giả/kế hoạch nhãn/split/đánh giá sẵn; cần dữ liệu thực và trao đổi giảng viên |
| G1-12 | Hoàn thành | Hoàn thành báo cáo đợt thực thi; kết luận **Chưa đạt M1**, không phải hoàn thành toàn GĐ1 |

Bằng chứng, lệnh thực tế, lỗi và bước tiếp theo nằm trong [report GĐ1](../report/phase-01-foundation-report.md). Không mở các quyền/API nghiệp vụ chưa chốt để bù phần thiếu cloud.

## Chốt danh sách còn lại của GĐ1 cho một người

**Danh sách lịch sử, đã được thay bằng ưu tiên localhost cuối tài liệu.** Không thực hiện G1-R03 hoặc phần deploy/redeploy cloud của G1-R04/R05/R07 lúc này.

Người phụ trách toàn bộ: **Chủ dự án**. Thứ tự dưới đây thay cho chia việc A/B/C; không đặt ngày hoàn thành giả khi chưa rõ quyền dịch vụ và giờ làm. Việc sửa tài liệu nhân sự đã hoàn thành; không dùng yêu cầu nhiều thành viên làm điều kiện chặn nữa.

| ID đóng việc | Gắn task | Việc cần làm và đầu vào | Điều kiện đóng / bằng chứng |
|---|---|---|---|
| G1-R01 | G1-01/02 | Quyền nghiệp vụ ưu tiên đã chốt; người dùng sẽ cung cấp dịch vụ sau qua checklist S01–S12. Số giờ cá nhân là thông tin lập lịch cần bổ sung, không phải yêu cầu có thêm người | Mục quyết định G1-02 đã đóng; phần dịch vụ đóng khi có trạng thái/quyền truy cập và kiểm chứng thật, không yêu cầu secrets trong chat |
| G1-R02 | G1-03/09 | Tự review diff local, publish theo quyết định của chủ dự án, chạy CI đúng commit | Commit SHA và run frontend/backend/Docker success; dựng lại theo README trên môi trường sạch, không thay bằng xác nhận ba người |
| G1-R03 | G1-05/06/09 | Chuẩn bị demo HTTPS, PostgreSQL 16 cùng major, private storage và DP certificate; migration bằng bước riêng | URL demo/version/migration thật; login/logout/CSRF/multipart; chính chủ đọc file, người khác bị chặn; runtime không dùng quyền DDL |
| G1-R04 | G1-07/09 | Xác nhận model/giá/kỳ/quota và phần ngân sách còn lại; cấu hình DeepSeek backend | Một luồng login→private file→AI thật trên demo; usage/chi phí thực, khóa ngừng dispatch và quota; timeout không giải phóng chi phí chưa rõ |
| G1-R05 | G1-05/06/07/09 | Thử restart/redeploy và phục hồi DB + objects + DP certificate trên môi trường cô lập | File/cookie/job đúng sau restart; worker mất lease không publish; biên bản restore, không reset DB dùng chung hoặc giả file rỗng |
| G1-R06 | G1-06 | Kiểm chứng giới hạn parser trên file khó, PDF scan thật/không text, DOCX nhiều phần; xử lý hard timeout trước upload Internet | Kết quả theo mẫu có nguồn/định dạng/ngôn ngữ, timeout được kiểm soát; ghi lỗi và phương án cụ thể; không mở OCR |
| G1-R07 | G1-10 | Kiểm chứng search thật một kỹ năng không PII và quyền email/domain/sender | URL thật + credits + điều khoản lưu/cache; kết quả kiểm tra sender hoặc phương án xử lý rõ. Email khôi phục sản phẩm hoàn tất GĐ2, không kéo toàn chức năng đó vào GĐ1 |
| G1-R08 | G1-11/12 | Kiểm kê bộ CV/JD mẫu được phép dùng, kế hoạch bổ sung dữ liệu đại diện, trao đổi rubric/lịch với giảng viên; đối chiếu lại sáu điều kiện M1 | Manifest và kế hoạch nhãn/split, bằng chứng trao đổi (không tự nhận được duyệt); GĐ1 có thể dùng mẫu giả ghi nhãn đúng, không đặt thêm số CV thật/nhãn cuối kỳ làm điều kiện M1 |

**Phân loại để không mở rộng GĐ1:** API nghiệp vụ hoàn chỉnh ở GĐ2, baseline ở GĐ2, revisions/xác nhận và trình tạo CV sản phẩm ở GĐ3, matching ở GĐ4, nguồn học/AI viết CV hoàn chỉnh ở GĐ5. Trong GĐ1 chỉ thử tích hợp sớm, ghi quyết định/thiết kế đủ cho phần kế tiếp; không yêu cầu hoàn thiện mọi bảng/API tương lai mới đóng nền tảng.

**Quyết định đã chốt:** một người thực hiện, chủ dự án tự quản lý repository/dịch vụ/ngân sách; stack và khoảng 4 USD/tháng giữ nguyên; UI tiếng Việt, quyền khách/admin/CV ẩn liên hệ, một role khi đăng ký, owner công ty, nộp lại/block theo tin, status matrix L06 xác nhận ngày 16/10 và lưu/xóa 90/7/14 ngày đã ghi về 01/04. Lịch 12 tuần vẫn là mục tiêu cần rà theo giờ thực tế. **Còn thiếu đầu vào:** trạng thái/quyền dịch vụ người dùng sẽ cung cấp sau; theo [checklist dịch vụ](../phase-01-services-checklist.md). Các quyết định kỹ thuật/đánh giá còn lại được gắn đúng giai đoạn, không hỏi lại chính sách đã xác nhận.

CI được kiểm tra lại: remote vẫn ở `bdb3aeb`; run `38718678231` success. Các kết quả 19 backend tests, 3 frontend tests, E2E và PDF là bằng chứng đợt thực thi trước; đợt sửa tài liệu không chạy lại API thật hoặc toàn bộ bộ test.

## Quyết định sản phẩm đã xác nhận

1. UI tiếng Việt; đầu vào CV/JD Việt–Anh; khách chỉ JD công khai, CV phải đăng nhập/đúng quyền; admin không mặc định đọc private.
2. Đăng ký chọn một role Candidate hoặc Recruiter, Admin cấp riêng; mỗi công ty một recruiter owner.
3. Ứng viên khác chỉ xem bản CV ẩn liên hệ đã xác minh; chưa có bản an toàn thì không trả file gốc. Recruiter còn quyền xem bản có liên hệ.
4. Recruiter đọc snapshot đơn đã rút/bị loại tối đa 90 ngày, trừ yêu cầu xóa tài khoản thu hồi sớm; nộp lại tạo đơn mới nếu không có đơn hiệu lực hoặc block ở tin đó. Block/bỏ block chỉ đúng tin, không ảnh hưởng tin khác.
5. Xóa tài khoản: thu hồi quyền ngay; 7 ngày hủy yêu cầu; sau đó purge/ẩn danh dữ liệu chính; backup hết hạn tối đa 14 ngày tiếp theo. [Chính sách chi tiết](../phase-01-data-retention.md).

Quyết định đã đóng ở mức yêu cầu. L05/L06 đã kiểm tra snapshot mỗi lần nộp, owner scope, direct file ID bị chặn, recruiter hết quyền sau 90 ngày, status transition, note owner-only, block đúng tin và reapply. L07 đã kiểm tra selection CV public, recruiter original, candidate derivative, ẩn liên hệ, link version cũ và upload version không auto-public. L08 đã triển khai local hủy/purge/restore theo 7/14 ngày, dọn application terminal ở ngày 90 và dọn upload pending không tham chiếu sau 24 giờ; bằng chứng và giới hạn local được ghi ở report mục 17.

## Ưu tiên hiện tại — localhost

M1 theo [roadmap đã cập nhật](../../project-docs/03-roadmap.md) chưa nghiệm thu đầy đủ; không tiếp tục chờ cloud để xây các luồng local độc lập. Chủ dự án vẫn là người thực hiện duy nhất.

Cập nhật 16/10/2026: L01–L13 có các lát implementation/test local; `JobDataRevisions` vẫn là migration thứ 15, L12/L13 không đổi schema. L10 bao phủ revision kỹ năng CV; L11 bao phủ yêu cầu JD có source/evidence và xác nhận recruiter owner; L12 bổ sung diagnostic/evaluator overlay giữ baseline score/order; L13 thêm candidate-entered CV schema v2, `simple-v1` preview/download PDF, revision bất biến và version file mới để mở lại/sửa. `./scripts/test.ps1` đạt 52 backend PostgreSQL tests, 3 frontend cùng lint/typecheck/build; E2E `cv-builder.spec.ts` đạt 1 test. PDF synthetic hai trang A4 được render; kiểm tra pypdf xác nhận text Unicode có thể trích xuất và có số trang. Docker `careergap:g3-l13-local-20261016` build thành công; working tree chưa được CI xác nhận/publish. Bằng chứng L08 ở mục 17, L09 ở mục 18, L10 ở mục 19, L11 ở mục 20, L12 ở mục 21 và L13 ở mục 22 của [report](../report/phase-01-foundation-report.md). Chưa có dữ liệu thật/nhãn đã duyệt nên không tuyên bố chất lượng hay nghiệm thu M1/M2/M3. Bước kế tiếp theo [kế hoạch](next-local-development-plan.md) là L14 trong GĐ3: mở rộng extraction và rà soát CV ngoài skills v1, giữ provenance đúng loại.

| Thứ tự | Công việc | Điều kiện / bằng chứng |
|---|---|---|
| 1 | Rà diff hiện có, dựng lại React/API/PostgreSQL local theo README | Ghi môi trường, commit hoặc diff, lệnh và kết quả thực; CI cũ không chứng nhận thay đổi mới |
| 2 | Đóng các khoảng trống nền tảng local: parser, quyền file, restart/job/quota, kế hoạch dữ liệu | Kiểm thử theo rủi ro và đối chiếu sáu điều kiện M1 mới; không dùng kết quả fake làm bằng chứng AI thật |
| 3 | L09 baseline retrieval hai chiều trên localhost | API/UI, quyền public/private và evaluator smoke đã có; 47 backend tests, frontend checks và `applications.spec.ts` E2E đạt. Chất lượng trên dữ liệu thật và CI working tree chưa có; smoke không nghiệm thu M2 |
| 4 | L10 revision kỹ năng CV có xác nhận | 48 backend tests, 3 frontend tests, `resumes.spec.ts` + `applications.spec.ts` 2 E2E; migration đã áp dụng. Chỉ skills schema v1; matching chưa dùng revision, không nghiệm thu M3 |
| 5 | L11 revision yêu cầu JD | 49 backend tests, 3 frontend tests, `jobs.spec.ts` + `resumes.spec.ts` + `applications.spec.ts` 3 E2E; migration đã áp dụng, Docker build đạt. Parser L11 tách dòng/gợi ý từ khóa local; không có đánh giá chất lượng |
| 6 | L12 diagnostic revisions/evaluator cạnh baseline L09 | 51 backend tests, 3 frontend tests, lint/typecheck/build; `applications.spec.ts` E2E 1 passed; Docker build và evaluator synthetic overlay đạt. Chỉ count term overlap theo confirmed revisions; không score/rank, không thay L09, không có nhãn thật |
| 7 | L13 CV builder schema v2 và PDF `simple-v1` | 52 backend tests, 3 frontend tests, lint/typecheck/build; `cv-builder.spec.ts` 1 passed. PDF E2E 2 trang A4, Unicode/text/page numbers trích xuất được và render xem; không migration. Chỉ 1 template thử, chưa mở AI extraction ngoài skills v1; Docker build đạt, chưa có CI cho working tree |
| 8 | Đánh giá, sửa lỗi và bàn giao bản localhost | Ba luồng chính hoàn chỉnh; có bằng chứng build/test, chất lượng và giới hạn; dữ liệu/khôi phục kiểm tra local |
| Hoãn | Hosting, DNS, cloud migrations, private storage production, chứng thư, redeploy/restore cloud | Chỉ mở lại sau khi sản phẩm hoàn thiện local và người dùng yêu cầu; không xóa hoặc thay đổi dịch vụ/dữ liệu hiện có |

Thiếu thông tin hosting/domain không chặn công việc localhost. DeepSeek/search/email vẫn có các kiểm chứng thật cần hoàn thành để công nhận sản phẩm đầy đủ; fake chỉ giúp xây và kiểm thử logic. Không tự gọi API trả phí trong đợt cập nhật tài liệu.
