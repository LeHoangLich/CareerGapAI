# Vận hành và kiểm tra lại bản localhost

## Phiên bản

Runtime E giữ source/DLL đã freeze ở M5. Bản dựng sạch dùng cùng source, rebuild riêng cho môi trường dùng thử; không gán hash DLL rebuild cho DLL đo DeepSeek. Git working tree chưa có commit bàn giao. Source archive có manifest đầy đủ hơn inventory 114 runtime files, bổ sung cấu hình frontend, CSS, font/OFL và lockfiles. Không chứa `.env`, credentials, corpus private, bin/obj/node_modules hoặc dữ liệu DB thật.

README gốc hướng dẫn phát triển; [hướng dẫn dùng thử](huong-dan-dung-thu.md) dùng các cổng riêng 5089/5179/55442 trên máy hiện tại. Lệnh launcher chỉ đọc cấu hình Fake theo allowlist, không nạp key trong `.env`. Chế độ `fault` tắt AI admission, không mô phỏng toàn bộ lỗi mạng/provider.

## Kết quả và cách kiểm tra lại

Xem `project-docs/evaluation/stage-e-results-2026-10-27.json` và private `.local/stage-e-20261027/`. Giữ `baseline-frozen.json`, captures trước sửa và envelope đúng hash. Lượt `baseline.json` đầu sai cấu hình bị loại, không dùng tính kết luận. Final evaluator từ chối ma trận có sáu cặp thất bại; không sửa capture để ép ra metric.

Các script thực thi/repair/setup/settlement là nhật ký thao tác **một lần**, không phải lệnh khởi động demo. Không rerun `runtime.py run`, `freeze-v2.py`, `setup`, `settle.py` hoặc `prepare-trial.py` để “khôi phục” trạng thái trước đo. Chúng có thể tạo phiên bản, thay ledger hoặc tiêu API. Tập cuối hiện tại đã được xem; lần phân tích sau là tái phân tích/regression, không gọi final độc lập mới.

Snapshot ledger trước và sau usage được giữ riêng. Preflight trước chạy có trạng thái ready tại thời điểm đó; checker lịch sử tham chiếu live ledger có thể phát hiện hash đổi sau settlement hợp lệ. Không sửa ledger về số cũ để làm checker xanh. Cần manifest/accounting phiên bản mới nếu đăng ký thí nghiệm tiếp theo.

## Backup và restore

Luôn dừng API/worker trước khi chụp DB và objects để có snapshot phối hợp. DP keys ở bảng DataProtectionKeys trong dump. Giữ deletion ledger tối thiểu cập nhật **sau** backup nếu có yêu cầu xóa mới. Bản backup local chứa dữ liệu nhạy cảm, không đưa vào repo/share; giữ quyền truy cập riêng.

Quy trình DB ứng dụng chính đã có `scripts/backup-local.ps1`, `export-deletion-ledger.ps1`, `restore-local-backup.ps1`, `expire-local-backups.ps1`. Chúng có guard DB/loopback/Local storage; không đổi tên DB hoặc nối cloud để vượt guard. Kiểm E mới dùng harness riêng trên synthetic DB và CLI replay hiện có, không chạy các script đó trên DB chủ dự án.

Sau restore cô lập, trước mở API:

1. Đối chiếu dump, số dòng/hash bảng, objects và DP keys với manifest.
2. Áp migration phù hợp bằng bước riêng nếu cần. Không reset DB thật.
3. Replay ledger mới nhất, xử lý đủ purge/object errors rồi mới mở quyền.
4. Kiểm cookie cũ bị từ chối, fresh login, file/job/snapshot và tài khoản pending/deleted. DP key được phục hồi không có nghĩa cookie cũ được phép sống lại.

Lượt E: 19 migrations/43 tables/18 objects/5 ledger entries đạt; pending mới sau backup không hồi sinh. Chưa kiểm S3/offsite/Production certificate. Không mở API trực tiếp vào backup cũ mà bỏ qua ledger.

## Checklist lưu xóa thủ công

Thực hiện mỗi ngày có vận hành, trước/sau restore và sau sự cố storage. Chưa cài scheduler hoặc nhắc tự động; việc máy tắt hoặc bỏ quên có thể vượt thời hạn.

- Giữ chính sách đơn 90 ngày, xóa account chờ 7 ngày, expiry backup trong 14 ngày sau purge. Worker account/application/orphan chỉ chạy khi API/worker hoạt động; theo dõi trạng thái retry, không báo đã xóa chỉ từ enqueue.
- Upload pending/delete_pending không tham chiếu: worker hiện có grace 24 giờ, row lock/lease/retry. Ready file hoặc file còn version/snapshot hợp lệ không bị xóa bởi bảo trì failed.
- Quy tắc E theo ủy quyền hoàn thiện: upload **failed không tham chiếu, created_at đủ 7 ngày** được chuyển sang delete_pending thủ công; thời hạn tính từ thời điểm tạo upload, không phải thời điểm thất bại. Xem trước rồi mới Apply; rà kết quả batch trước lần kế tiếp.

  ```powershell
  # Điền đúng container/user DB local của môi trường đang bảo trì.
  ./scripts/queue-expired-failed-files.ps1 -ContainerName <container> -Database careergap -DatabaseUser careergap
  # Chỉ sau khi kiểm kết quả dry-run:
  ./scripts/queue-expired-failed-files.ps1 -ContainerName <container> -Database careergap -DatabaseUser careergap -Apply
  ```

  Công cụ chỉ xếp hàng metadata, batch tối đa 100. Worker hiện có mới xóa object/retry; kiểm lại DB và file trước khi báo hoàn tất. Không xóa file vô danh bằng cách dò bucket hoặc xóa trực tiếp theo tên người dùng. Đợt E chỉ kiểm trên restore synthetic, chưa chạy bảo trì DB thật.

- Export ledger cập nhật và chạy `expire-local-backups.ps1 -DeletionLedgerPath <private-ledger-path>` theo chính sách. Đây là lệnh xóa thật backup đủ điều kiện, không có dry-run; đọc manifest/đường dẫn trước khi chạy. Không dùng đồng hồ giả trên thư mục backup thật. Giữ ledger cần cho restore riêng với kiểm soát truy cập; không giữ raw CV/output quá mục đích cho phép.
- Với archive thử nghiệm private, ghi ngày rà lại/quyền còn hiệu lực; nếu phải xóa dữ liệu nguồn thì giữ metadata/hash tối thiểu được phép, đánh dấu không còn replay được. Không coi quyền local evaluation là quyền lưu hoặc gửi không giới hạn.

## Lỗi còn mở

CV extraction 0/2, pair 2/8 trong thí nghiệm thật và topic marketing false positive cần chẩn đoán trên dữ liệu tuning/synthetic có quyền. Không tăng output cap, sửa prompt/validator theo final rồi giữ nguyên version. Ghi phiên bản mới và phần đánh giá phải làm lại; chưa có raw body lỗi để kết luận field nào sai. Không công nhận M6/M3 representative quality từ demo Fake.
