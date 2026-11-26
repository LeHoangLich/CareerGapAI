# Đồng bộ bộ bàn giao localhost — 29/10/2026

Đã tạo bộ riêng `project-docs/handoff/stage-e-20261029/`, giữ nguyên gói/report/sourceZIP ngày27/10. Phạm vi được owner cấp là chốt mã, hướng dẫn, slide/video, cài sạch/Docker và diễn tập; **M6 chưa nghiệm thu** khi thiếu phản hồi giảng viên và nhận xét chất lượng.

## Mã nguồn và kiểm thử

Không sửa frontend/backend nghiệp vụ trong lượt này. Chốt các thay đổi UI multi-view và Style Guide do owner yêu cầu ở các lượt trước. Bộ hồi quy mới `.local/frontend-step3-20261029-184738-7756/` completed/cleanup: frontend lint/typecheck/build, unit11/11, backend PostgreSQL159/159 và E2E21/21 đạt, không skipped/flaky. Bộ E2E có cả kiểm tích hợp API thật/Fake worker và kiểm UI dùng mock; không gọi cả21là provider thật. Runner tests6/6 và browser preflight đạt.

Đầu lượt Docker chưa chạy; một lần lỗi vì thiếu pipe, một lần chờ engine rồi thất bại trước postgres-image. Log cũ giữ nguyên, không có suite/API mutations được suy là đã chạy ở các lượt đó. Docker Desktop báo socket AF_UNIX cũ không truy cập được; đã dừng các process Docker thuộc bản cài, đổi tên giữ lại đúng thư mục runtime socket Docker/run và docker-secrets-engine rồi khởi động lại thành công29.5.3. Không factory-reset, xóa container/volume hoặc nâng phiên bản Docker. Đây là quan sát khôi phục local, không kết luận nguyên nhân kernel độc lập.

## Bản demo và diễn tập

Thêm launcher `scripts/handoff-demo.py`, chỉ tạo synthetic DB/container/volume riêng, loopback web5190/PG55449. Chỉ Fake và key trống, không nạp.env/appsettings.Local, không sửa demo5183/ứng dụng55432. Static production frontend nằm cùng runtimeAPI, không cần Vite. Candidate/recruiter/admin được seed bằng tài khoản random private. Lỗi restore assets của bản helper đầu được sửa và giữ log; launcher sau sửa đã build/migrate/start/fault/restart được.

Diễn tập tự động trên production frontend + API thật local: công ty/tin/JD được xác nhận, writing Fake được duyệt vào draft, PDF tạo/lưu đúng version mới, retrieval và matching ở cột phải, nộp CV vẫn private, recruiter tải đúng bytes snapshot và cập nhật sàng lọc/ghi chú. Bản quay lại chờ ghi chú nằm trong danh sách đã lưu và textarea được xóa; không lấy nội dung đang nhập làm bằng chứng lưu. Video có banner synthetic/Fake trong chính hình quay. AIoff trả thông báo rõ, file200 và session giữ nguyên. Đây không phải phản hồi owner/giảng viên hoặc quality benchmark.

## Backup, vận hành và package

Helper `scripts/handoff-backup.py` backup offline đúng demo mới và verify vào database restore riêng: **43bảng, 4objects**, hash dữ liệu và DataProtectionKeys khớp. DBrestore giữ offline. Không có mutation sau backup trong phép thử này; replay ledger xóa mới/cookie cũ/quyền đọc dùng bằng chứng ngày27/10 với backend source không đổi, không giả đã chạy lại. Hướng dẫn phân biệt backup demo với runbook dữ liệu thật và retention thủ công.

Gói source mới chứa manifest/hash từng file, dependency lock/config/font/PostCSS/worker và tài liệu cần chạy; không đóng gói node_modules/build/.env/raw corpus/tài khoản/ảnh account. Cài sạch và Docker có biên nhận riêng khi chốt; nếu kiểm thất bại thì không đánh dấu đạt trong manifest. Mã nguồn cuối và thành phần commit được ghi private, giữ file Word đã sửa của owner, thư mục skill và các output không thuộc package.

## Kết quả nghiên cứu và tài chính giữ nguyên

E đầu: CV0/2, pair2/8, writing4/4; regression sau sửa trên tập đã xem: CV2/2, pair8/8, F1=0,6231884, tiêu chí34/44 theo nhãn Codex, CV→JD nDCG1,000→0,917. Không có improvement claim/human labels mới. NativeCVViệt/JDViệtCNTT thiếu, nhãn benchmark human0. UI/PDF mới không sửa method/schema/dataset hoặc cấp transfer cho tài liệu mới.

Owner U1/U5 và actor lượt26/10 đã ghi riêng. Tavily dashboard28/10 17:13VN tổng4/1000=D2+E2, PAYGoff; đối soát tổng hoàn thành, Usage chi tiết bị khóa và ngày/múi giờ reset chính xác chưa hiện. DeepSeek ledger0,11USDspent+0,03hold, guardedremaining1,86/2USD giữ nguyên; không xóa unknown, không gọi lại paid/search/email hoặc reset quota.

## Kết luận M6

[Ma trận M6](../handoff/stage-e-20261029/nghiem-thu-M6.md) đối chiếu cả6điều kiện roadmap. Phần bàn giao kỹ thuật có bằng chứng mới; còn **giảng viên dùng thử và nhận xét phạm vi/chất lượng**. Agent không tạo chữ ký, biên bản hay quyết định chấp nhận thay người đánh giá. Không tuyên bố100%/M3representativequality/M6accepted hoặc deploy. [Bộ hiện hành](../handoff/stage-e-20261029/README.md).
