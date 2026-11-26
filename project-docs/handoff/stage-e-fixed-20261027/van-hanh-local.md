# Vận hành bản E sau sửa

## Chạy và dừng bộ dùng thử trên máy hiện tại

Tại `D:\CareerGapAI`, bật Docker rồi chạy:

```powershell
python .local/stage-e-followup-20261027/trial-runtime.py start
```

Mở `http://127.0.0.1:5183/`. API 5093 dùng database riêng `careergap_restore_handoff_fixed_20261027` trên PostgreSQL persistent cổng 55442. File ở `.local/stage-e-followup-20261027/durable-demo-files`. Không dùng DB test tmpfs hoặc DB ứng dụng thật 55432. Launcher đặt AI/search/email Fake và key trống bằng allowlist, không nạp `.env`.

Chế độ `fault` tắt AI admission; đã kiểm 503, không tạo job mới, vẫn đọc CV được. Nó không mô phỏng mọi loại timeout/mạng/provider. Dừng riêng API/Vite bằng `trial-runtime.py stop`; có thể dừng container riêng bằng `docker stop careergap-step6-eval-20261027`, dữ liệu giữ trên volume. Không dùng `docker compose down -v` để kết thúc demo.

Gói source là bản cho tái dựng; launcher private bên trên chỉ dành cho môi trường đã chuẩn bị trên máy hiện tại. Khi chuyển máy, dùng README root để tạo `.env` riêng, restore lockfiles, migrate vào DB riêng và tạo tài khoản synthetic mới. Không chia sẻ nguyên `.local`.

## Phiên bản và cách đọc bằng chứng

Phương pháp CV sau sửa là `cv-fields-bounded-v2`, pair là `candidate-requirements-v4`, schema/public DTO giữ tương thích. Bản v4 có 159 backend/9 E2E/3 frontend, Docker Linux mới, restore 43 bảng/11 files/3 ledger. Prompt cũ vẫn được nhận diện khi đọc job cũ; không gán lại hash method mới cho output trước sửa.

DLL và cấu hình thực nghiệm mới được ghim trong `.local/stage-e-followup-20261027/new-runtime-manifest-v4.json`; kết quả, snapshot trung gian và kế toán giữ private. Gói source/commit bàn giao có thêm thay đổi ignore chỉ để loại thư mục phát sinh khỏi Git/Docker, không làm đổi C# đã đo. Windows DLL và Linux image là hai artifacts khác nhau.

Kết quả đầu E độc lập ban đầu vẫn là 0/2 CV, 2/8 pair, 4/4 writing. Kết quả sau sửa 2/2 + 8/8 và F1/ranking là regression trên tập đã xem. Không rerun setup, diagnostics, regression, search hoặc settlement script để khởi động demo; chúng là thao tác một lần có thể tiêu API hoặc thay chứng cứ.

## Backup và restore

Giữ quy trình [runbook E đầu](../stage-e-20261027/van-hanh-local.md): dừng API/worker để chụp đồng bộ DB+objects, giữ DP keys và deletion ledger cập nhật. Restore vào DB local cô lập, đối chiếu hash rồi replay ledger trước mở API. Không bypass guard của các script backup chính để nối cloud/DB khác.

Bản mới đã kiểm fresh login, private file/job, cookie cũ401 và pending account403. Demo lưu bền cũng được đối chiếu 43 bảng trước replay. Chưa có chứng cứ S3/offsite/certificate Production.

Giữ 90 ngày đơn/7 ngày account/14 ngày backup; failed file không tham chiếu đủ 7 ngày từ upload chỉ queue thủ công qua `scripts/queue-expired-failed-files.ps1`, mặc định dry-run rồi `-Apply`, worker hiện có mới xóa/retry. Expiry backup và queue failed chưa có scheduler; cần kiểm mỗi ngày có vận hành và trước/sau restore. Bản script/test đã có không bảo đảm SLA khi máy tắt hoặc bỏ quên.

## Raw chẩn đoán và secrets

`Ai__PrivateDiagnostics=true` chỉ được dùng có chủ đích trong Development trên máy local khi có quyền dữ liệu. Mặc định tắt; bản demo cũng tắt. Testing/Production không ghi dù flag bật. Không có API tải raw chẩn đoán. Log chỉ ghi loại lỗi; không có quote, token hay API key.

Raw nằm dưới `.local/ai-diagnostics` và các bản chép phân tích private có inventory trong `.local/stage-e-followup-20261027/diagnostic-retention.json`. Rà/xóa tối đa 7 ngày từ đợt này, hoặc sớm hơn khi mục đích/quyền yêu cầu. Giữ receipt/hash tối thiểu được phép để chứng minh đã dọn; không để lại bản sao raw trong forensic packet sau khi xóa bản gốc. Đây là bảo trì thủ công, chưa có lịch nền.

Không commit `.env`, private corpus/output, account packet, reset links, certificate hay file Word tiến độ của chủ dự án. Thư mục phát sinh `frontend/%SystemDrive%` đã bị loại khỏi Git và Docker context, không bị xóa.

## Hạn mức

Sau sửa: spent0,11USD + holds0,03USD, guarded1,86USD trong trần phát triển2USD. Giữ journal unknown cũ/reservation, không hoàn hoặc reset theo DB/kỳ. Tavily mới2credits có response/journal, Usage API chưa phản ánh tổng mới; không mở thêm allowance từ sự chênh lệch đó. Email mới0. Không bật provider thật trong buổi dùng thử để làm demo “đẹp hơn”.
