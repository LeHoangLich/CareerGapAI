# Vận hành bản demo 30/10

## Runtime và dữ liệu

Web/API ở `127.0.0.1:5190`, database loopback55449 thuộc container/volume `careergap-handoff-demo-20261029`. Tài khoản, config, storage và DP keys trong database được giữ. Database ứng dụng55432 và môi trường cũ khác không thuộc lần chuyển này.

- Runtime cũ: `.local/handoff-demo-20261029/runtime`.
- Runtime mới: `.local/handoff-demo-20261029/releases/20261030/runtime`.
- `python scripts/handoff-demo.py start` mặc định chọn release20261030; có thể truyền `--release` tường minh.
- Launcher build khi runtime của release chưa tồn tại, sau đó tái sử dụng. Sửa source không tự thay binary; cần một release mới và kiểm chứng riêng. Không gán kiểm thử Windows cho DLL Linux.
- `.local/handoff-demo-20261029/process.json` ghi PID, release, runtime và assembly SHA-256. Stop kiểm command line thuộc vùng demo trước khi dừng.

## Backup/restore và quay lại

Đã tạo backup offline trước khi chuyển runtime. Thư mục backup và receipt private ghi trong báo cáo; không phát database hoặc tài khoản trong gói public. Có thể chạy `python scripts/handoff-backup.py backup` rồi `verify`; API bị dừng khi backup, verify tạo database restore riêng và không mở API trên dữ liệu restore. Sau đó start lại release mong muốn.

Quay về binary cũ trên máy đã có runtime29/10:

```powershell
python scripts/handoff-demo.py start --release 20261029
```

Chuyển lại bản mới:

```powershell
python scripts/handoff-demo.py start --release 20261030
```

Hai runtime dùng cùng dữ liệu synthetic, không copy ngược database hoặc bỏ dữ liệu mới. Lần sửa chỉ thêm API/UI đọc, không migration/schema mới. Nếu máy mới chưa có runtime29/10, launcher từ chối chọn release20261029, không build source hiện tại dưới nhãn lịch sử. Muốn tái tạo nguồn lịch sử phải dùng gói source29/10 riêng và vùng kiểm tra riêng.

Nếu cần tắt cả PostgreSQL sau khi dừng API: `docker stop careergap-handoff-demo-20261029`. Không tự dừng khi người dùng đang dùng thử. Helper nền từ phiên diễn tập trước thuộc công cụ của phiên đó; không phải thành phần bắt buộc của launcher và không được coi là bảo đảm dịch vụ chạy liên tục.

Provider và ledger thật không đổi: AI/search/email demo Fake, key trống, không đọc `.env`. Fake/test_credit không phải USD hoặc Tavily credits. Phần đánh giá giảng viên đang hoãn; không dùng runtime mới hoặc video để tự chốt M6.
