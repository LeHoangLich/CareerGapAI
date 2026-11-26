# Vận hành và dữ liệu bản bàn giao

## Phân tách môi trường

Launcher `scripts/handoff-demo.py` tạo container `careergap-handoff-demo-20261029`, volume cùng tên, database `careergap_test_ui_handoff_demo`, loopback PostgreSQL55449 và web5190. Mật khẩu/tài khoản, file CV, log và build nằm trong `.local/handoff-demo-20261029/`. Đây là dữ liệu synthetic mới, độc lập database ứng dụng55432 và demo cũ55442/5183.

Mọi provider trong launcher là Fake, key trống, AI private diagnostics tắt. `fault` chỉ tắt AI; không gửi email/search/DeepSeek. Các lượt thử thật trước đây giữ evidence riêng: Tavily đã đối soát tổng4credits ngày28/10; các cap thử đã đóng, không cấp thêm theo database mới. Ledger phát triển DeepSeek được giữ riêng, không được thay bằng Fake test_credit.

Launcher giữ API trong thư mục runtime đã build, phục vụ static frontend tại cùng origin. Khi thay source sau bàn giao cần tạo phiên bản runtime/bằng chứng mới; không gán kết quả cũ cho file đã thay. Launcher stop kiểm command line trước khi dừng process của mình. Không tự xóa database/volume hoặc đọc .env. Sau khi thử xong có thể giữ container hoặc dùng `docker stop careergap-handoff-demo-20261029` để tiết kiệm tài nguyên.

## Backup và restore

Backend/storage/schema không đổi so với các bằng chứng restore đã ghim: 19 migrations,43tables, objects+DataProtectionKeys, ledger replay và quyền sau restore. Báo cáo bản mới ghi rõ phần reuse theo source và phần được thử mới.

Demo có thể backup/verify bằng `python scripts/handoff-backup.py backup` và `python scripts/handoff-backup.py verify`. Helper chỉ xử lý database/container thuộc demo mới, dừng API demo trước snapshot, giữ dump+objects+manifest trong `.local/handoff-demo-20261029/backups/`. Verify tạo database restore mới, đối chiếu nội dung bảng và hash file, không mở lại API hoặc dùng database ứng dụng. Sau verify, mở lại demo bằng lệnh start.

Với database ứng dụng thực, dùng runbook `scripts/backup-local.ps1`, `export-deletion-ledger.ps1`, `restore-local-backup.ps1` và `expire-local-backups.ps1` sau khi đọc điều kiện offline và đường dẫn riêng. Không trỏ các helper demo vào dữ liệu thật. Restore dữ liệu thật phải lấy ledger xóa mới nhất và replay trước khi mở API.

## Lưu/xóa và bí mật

Giữ các chính sách đã triển khai: quyền terminal application90ngày, account deletion7ngày, backup expiry14ngày theo runbook, failed-file queue7ngày theo upload. Bảo trì local là thủ công và existing worker, không có scheduler/SLA khi máy tắt. Chạy dry-run, kiểm đối tượng và Apply theo đúng runbook.

Metadata search và raw diagnostics đã bật cho các lượt đánh giá cụ thể có thời hạn riêng7ngày theo inventory private. Không mặc định demo mới bật diagnostics. Phiếu có tên người đánh giá, screenshots tài khoản, key, token và bản CV/JD thật giữ private. Source/PPTX/video bàn giao chỉ chứa nội dung public và synthetic đã rà.
