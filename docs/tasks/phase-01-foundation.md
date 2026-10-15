# GĐ1 — Nền tảng, quyền và đánh giá

Ngày lập: 15/10/2026. Trạng thái ban đầu: **Chưa bắt đầu**. Lịch: tuần 1–2 tính từ ngày nhóm khởi động, chưa gán ngày thực tế.

## Mục tiêu và nguồn yêu cầu

Đạt M1 bằng luồng đăng nhập → tải/đọc file private đúng quyền → nhận kết quả DeepSeek có ngân sách; kiểm chứng triển khai và chuẩn bị dữ liệu đánh giá. Một lần tích hợp thành công không chứng minh chất lượng AI hoặc hoàn thành nghiệp vụ GĐ2–5.

Nguồn: [đặc tả](../../project-docs/01-system-specification.md), [công nghệ](../../project-docs/02-tech-stack-decisions.md), [roadmap — GĐ1/M1](../../project-docs/03-roadmap.md), [database](../../project-docs/04-database-design.md), [hướng dẫn agent](../../AGENTS.md).

Hiện trạng đầu đợt: chỉ có bốn tài liệu và AGENTS.md; chưa có Git, mã nguồn, migrations hay build/test. Không mặc định đã có tài khoản provider, cloud, domain, sender hoặc dữ liệu thật được phép sử dụng.

## Theo dõi thực hiện

Trạng thái: Chưa bắt đầu / Đang làm / Bị chặn / Hoàn thành. A = backend/hạ tầng; B = frontend/tích hợp; C = dữ liệu/kiểm thử. Đây là vị trí đề xuất, chưa gán tên và chưa xác nhận giờ làm của ba thành viên.

| ID | Người phụ trách | Phụ thuộc | Trạng thái ban đầu |
|---|---|---|---|
| G1-01 | A, B, C | Không | Chưa bắt đầu |
| G1-02 | C chủ trì | G1-01 | Chưa bắt đầu |
| G1-03 | A, B | G1-01 | Chưa bắt đầu |
| G1-04 | A, B | G1-02, G1-03; phần self độc lập | Chưa bắt đầu |
| G1-05 | A, B | G1-04 | Chưa bắt đầu |
| G1-06 | A, C | G1-04, G1-05 | Chưa bắt đầu |
| G1-07 | A, C | G1-04, G1-06 | Chưa bắt đầu |
| G1-08 | B | G1-05–07 | Chưa bắt đầu |
| G1-09 | A, B | Bắt đầu G1-03; nghiệm thu sau G1-08 | Chưa bắt đầu |
| G1-10 | B, C | G1-03 | Chưa bắt đầu |
| G1-11 | C | G1-01 | Chưa bắt đầu |
| G1-12 | A, B, C | Các kết quả trên | Chưa bắt đầu |

### G1-01 — Kiểm kê

- [ ] Ghi công cụ/phiên bản thực tế, trạng thái Docker/PostgreSQL và lệnh đã chạy.
- [ ] Xác định ai quản lý repository, cloud, DNS/sender và ngân sách; không đọc/in secrets.
- [ ] Xác nhận nguồn CV/JD, quyền dùng, lịch và giờ làm của thành viên.
- Đầu ra/nghiệm thu: danh sách có bằng chứng; mọi thông tin chưa cung cấp ghi chưa xác nhận.

### G1-02 — Quyết định và quyền

- [ ] Ghi ma trận quyền và danh sách quyết định còn mở: liên hệ CV công khai, khách/admin, UI, vai trò tài khoản, owner công ty, trạng thái, rút/nộp lại và lưu/xóa dữ liệu.
- [ ] Chỉ cập nhật quyết định được người dùng xác nhận vào tài liệu nguồn; gắn phần bị chặn tương ứng.
- Đầu ra/nghiệm thu: self-only không mở quyền công khai; phần chưa chốt không được diễn giải thành chính sách đã duyệt.

### G1-03 — Nền tảng

- [ ] Khởi tạo Git local, một Web API .NET 10, một project test và React/TS/Vite.
- [ ] Khóa phiên bản, lockfile, kiểm tra giấy phép dependency đúng phiên bản.
- [ ] PostgreSQL local cùng major CI/demo; cấu hình mẫu, ignore secrets và README lệnh thực tế.
- Đầu ra/nghiệm thu: restore/build/test và hướng dẫn cho máy khác; chỉ công nhận ba thành viên chạy được khi có xác nhận thực tế.

### G1-04 — API và schema

- [ ] REST /api, OpenAPI, DTO và ProblemDetails.
- [ ] Migrations theo lát: Identity/DP, candidate profile tối thiểu, file/CV versions, method/jobs/budgets/attempts.
- [ ] FK owner/version, numeric tiền, thời gian UTC, bất biến version và migration triển khai riêng.
- Đầu ra/nghiệm thu: PostgreSQL thật kiểm tra database mới và nâng cấp có dữ liệu; không tạo schema tương lai chỉ để đủ bảng.

### G1-05 — Tài khoản

- [ ] Identity/cookie, phiên hiện tại, CSRF, login/logout, kiểm tra account status/owner.
- [ ] Tài khoản giả tạo bằng lệnh kiểm soát, không mật khẩu cố định; không API tự cấp Admin.
- [ ] DP key bền vững, production bảo vệ bằng chứng thư và hướng dẫn backup khóa.
- Đầu ra/nghiệm thu: sai mật khẩu/CSRF/sai quyền bị chặn; kiểm tra qua proxy/redeploy riêng.

### G1-06 — File

- [ ] Upload private → xác minh → ready → CV version, không giữ transaction lúc upload.
- [ ] Giới hạn file/trang/text/giải nén/thời gian; không tải external resources hoặc chạy macro.
- [ ] Parser PDF/DOCX giữ nguồn trang/đoạn, thử Việt–Anh, nhiều cột/bảng, scan và file lỗi.
- [ ] Đọc theo ID qua backend kiểm tra chính chủ; object bất biến và xử lý upload dở.
- Đầu ra/nghiệm thu: hai ứng viên không đọc chéo; recruiter/admin không được mặc định xem private; file scan không có dữ liệu giả.

### G1-07 — Job và ngân sách

- [ ] Fake LLM/search/email mặc định; typed interfaces cho provider.
- [ ] PostgreSQL queue/lease/generation; idempotency key/hash; request trả 202, khác hash trả 409.
- [ ] Giữ ngân sách atomic, attempts/allocations, đối soát một lần; timeout giữ khoản chưa rõ; retry tối đa một lần trong dự trù.
- [ ] Validate schema và nguồn trước phát hành, kiểm tra quyền lúc gửi/trả kết quả; không chấm điểm giả.
- [ ] Thử DeepSeek thật có chủ đích sau khi có cấu hình/model/giá và ngân sách đã kiểm tra.
- Đầu ra/nghiệm thu: restart không mất job, worker mất lease không publish, usage thực được ghi; fake không được gọi là AI thật.

### G1-08 — Giao diện

- [ ] Đăng nhập, upload/mở CV riêng, tạo job, polling và đọc kết quả.
- [ ] Thể hiện loading/lỗi/quota và nhãn fake rõ; không lộ stack trace/lease/token.
- Đầu ra/nghiệm thu: luồng browser xuyên suốt; chỉ UI thử nghiệm tiếng Việt, chưa xác nhận ngôn ngữ sản phẩm.

### G1-09 — CI và demo

- [ ] CI frontend lint/typecheck/build, backend test với PostgreSQL thật và Docker build; dùng fake.
- [ ] Kiểm tra proxy, cookie/CSRF/multipart, DP và file sau restart/redeploy.
- [ ] Ghi URL/version/migration thật; nếu Vercel không phù hợp kiểm chứng React static trên .NET.
- Đầu ra/nghiệm thu: cả ba thành viên chạy được, CI thật pass và cloud có một luồng AI thật; chưa có tài khoản thì ghi bị chặn, không mua/nâng gói.

### G1-10 — Các thử nghiệm sớm

- [ ] Xuất PDF có font Việt, xuống dòng/phân trang và chọn/copy text.
- [ ] Search một kỹ năng không có PII, URL từ provider; kiểm tra credits/điều khoản cache.
- [ ] Kiểm tra quyền DNS/sender; fake email không chứng minh gửi thật.
- Đầu ra/nghiệm thu: bằng chứng hoặc phương án xử lý cụ thể cho từng thử nghiệm; không tự mua domain.

### G1-11 — Dữ liệu và đánh giá

- [ ] Manifest nguồn, giấy phép/đồng ý, ngôn ngữ, định dạng, nhóm tài liệu và mục đích.
- [ ] Schema nhãn bằng chứng, chia nhóm train-tuning/evaluation tránh leakage; demo tách đánh giá.
- [ ] Kế hoạch đánh giá trích xuất trước sửa, chọn tập/xếp hạng hai chiều và giải thích, baseline cùng dữ liệu.
- [ ] Danh sách cần giảng viên góp ý; không tự đặt ngưỡng hoặc ghi đã được duyệt.
- Đầu ra/nghiệm thu: dữ liệu giả được đánh dấu, không đại diện chất lượng AI; chưa có CV thật thì ghi rõ.

### G1-12 — Nghiệm thu/report

- [ ] Cập nhật kết quả từng task, lệnh/thư mục/kết quả và bằng chứng.
- [ ] Đối chiếu sáu điều kiện M1 trong roadmap theo Đạt / Chưa đạt / Chưa kiểm chứng.
- [ ] Sau đợt thực thi tạo [report](../report/phase-01-foundation-report.md), có chi phí/usage, lỗi, phụ thuộc và bước tiếp theo.
- Đầu ra/nghiệm thu: report trung thực; thiếu bất kỳ điều kiện M1 thì kết luận Chưa đạt M1.

## Nhịp và giới hạn

Tuần 1: quyết định, nền tảng, API/schema, luồng local fake và dữ liệu. Tuần 2: cloud, thử thật có giới hạn, restart, PDF/search/email và nghiệm thu. Hết tuần không tự hoàn thành.

Ngân sách khoảng 4 USD/tháng dùng chung cả nhóm/môi trường/thực nghiệm/demo; tách USD và search credit; không bật pay-as-you-go. Chỉ self/private trong đợt này; không mở API ứng tuyển/công khai CV hoặc tự chốt quyền còn mở. Không cắt tạo CV, nguồn học hay matching hai chiều khỏi các giai đoạn sau.

## Nhật ký thực thi

Sẽ cập nhật trong đợt thực thi và liên kết tới report; trạng thái ban đầu phía trên được giữ làm mốc kế hoạch.
