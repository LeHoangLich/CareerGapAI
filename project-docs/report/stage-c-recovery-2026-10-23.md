# Bổ sung giai đoạn C — Restart và backup/restore local

Ngày kiểm chứng **23/10/2026**, Asia/Saigon. Theo yêu cầu tiếp tục phần C còn thiếu sau khi chủ dự án bật Docker. Kế thừa [report C](stage-c-2026-10-22.md), không mở GĐ5/deploy hoặc gọi thêm DeepSeek.

## Kết quả

**Đã hoàn tất phần kiểm chứng restart và backup/restore local còn thiếu của C.** Có dump PostgreSQL, file lưu trữ, Data Protection keys, manifest SHA256 và bằng chứng khôi phục sang DB riêng. Điểm/nguồn/snapshot/idempotency/cache/quyền được kiểm qua API sau restart và restore. Không còn phần kỹ thuật C đang chờ Docker.

Chất lượng matching trên dữ liệu đại diện vẫn chưa được công nhận: corpus M3 hiện có nhãn Codex dự thảo, còn thiếu người rà và độ phủ; quyền transfer corpus đó vẫn false. Bộ dữ liệu lượt này là synthetic/Fake, không thay thế đánh giá chất lượng thật hoặc thẩm định rubric.

## Môi trường và nguồn dữ liệu

- HEAD local lúc chạy: `64fbc991833f42ffad6a101579026b6ad5414aaf`. Đây là commit hiện có do chủ dự án quản lý, khác SHA của lần C trước; không tự commit/push hoặc gán kết quả CI cũ cho SHA này.
- Đã build backend hiện tại: **0 warning, 0 error**. Bảy file runtime/evaluator trong manifest chốt C trước có hash không đổi. Không sửa runtime, dependency, schema hoặc thêm migration; vẫn **16 migrations**.
- Container cũ `careergap-stagec-20261021` được khởi động kiểm tra: **0 bảng public**; tmpfs cũ đã mất dữ liệu. Không thể khôi phục nguyên instance DeepSeek cũ bằng cách bật lại Docker. Capture/usage/method freeze của pilot ngày 21/10 vẫn được giữ riêng.
- Tạo **instance Fake mới**: PostgreSQL `16.10-bookworm`, container `careergap-stagec-20261023`, volume `careergap-stagec-20261023-data`, chỉ bind `127.0.0.1:55439`. API thử riêng `127.0.0.1:5081`, provider ép Fake và key rỗng; không đọc `.env`.
- Database nguồn `careergap` nằm trong container riêng này; không phải DB ứng dụng mặc định ở port 55432. Restore sang `careergap_restore_stagec_20261023` trong cùng instance riêng. Dữ liệu mới gồm 3 CV DOCX/3 JD synthetic, 4 tài khoản `example.invalid`, 3 application; không dùng corpus thật.
- Chỉ tạo/chấm 9 cặp Fake ở ba ngữ cảnh self/public/application. Thử cache tạo các job kết quả mới với key riêng, không phát sinh attempt. Chi phí instance nguồn: **12 test_credit** (3 extraction + 9 matching), reserved **0**; không phải USD. Không có method DeepSeek trong DB mới.

## Các bước và kiểm tra đã chạy

| Bước | Kết quả thực tế |
|---|---|
| Seed luồng nghiệp vụ | Upload → extraction Fake → xác nhận CV/JD → công khai CV → nộp đơn → **9/9 pair succeeded** |
| Restart PostgreSQL và API | **9/9 kết quả** giữ nguyên toàn bộ view đã lưu; idempotency cùng key giữ đúng job gốc |
| Cache sau restart | **9/9 cache hit**, capture có `calls=0`; không gọi lại Fake để chấm |
| Quyền sau restart | **18/18 lượt đọc sai requester** bị 404 ở pair/capture |
| Snapshot sau restart | **3/3 file application** khớp SHA256 file DOCX đã nộp |
| Backup nhất quán | Dừng API/worker; không có queued/running job; `pg_dump -Fc`, sao 6 object, ghi SHA256; ledger xóa nguồn rỗng |
| Restore DB riêng | **36/36 bảng** khớp cả số dòng và SHA256 dữ liệu trước khi mở API; gồm 16 migrations, **18 match results** (9 gốc + 9 cache clone), **1 DataProtectionKey** |
| Restore object | **6/6 object** khớp hash từ nguồn → backup → thư mục restore |
| API trên DB restore | **9/9 view** khớp; **9/9 cache hit không attempt**, **18/18 sai quyền** bị chặn, **3/3 application file** khớp hash |
| Thu hồi quyền sau restore | Ẩn CV trên DB restore chặn pair/capture/generic job public (3 đường đọc); self và application vẫn được phép |
| Đối soát sau restore | Export deletion ledger nguồn rồi chạy CLI `--replay-deletion-ledger` trên DB restore; ledger 0 entry, security stamps được đổi; **4/4 cookie cũ trả 401** |

So sánh 36 bảng được thực hiện trước các request kiểm thử tạo cache clone hoặc bước đổi security stamp. Sau đó DB restore có thay đổi kiểm thử được ghi rõ, không yêu cầu hash còn bằng dump ban đầu. Có một lượt lặp API trước thử thu hồi quyền; không cộng các lượt lặp thành số spec mới.

Đọc được bằng cookie thử trước replay chỉ dùng để kiểm chứng key Data Protection đã được khôi phục; sau đó đã chạy chính sách vô hiệu hóa session cũ và kiểm 401. Không đề xuất mở DB restore thực tế trước khi đối soát ledger/session. Bài thử này không thay thế hoặc nới guard của `restore-local-backup.ps1`; runbook mặc định vẫn chỉ làm việc với DB/port được chỉ định trong script.

## Artifact và tái lập

Tất cả dữ liệu thử nằm trong `.local/stage-c-20261023/`, bị Git bỏ qua:

| Artifact | Nội dung |
|---|---|
| `backup/database.dump` | Dump PostgreSQL thực của instance Fake mới, có Data Protection keys |
| `backup/objects/`, `backup/manifest.json` | 6 object và SHA256 dump/object; backup được giới hạn ACL cho chủ máy, SYSTEM và Administrators |
| `backup/deletion-ledger.json` | Sổ xóa xuất từ DB nguồn, 0 entry |
| `evidence/source-db.json`, `restored-db.json` | Số dòng/hash của 36 bảng trước khi mở API restore |
| `evidence/backup-restore-check.json` | Kết quả so sánh DB |
| `evidence/restart.json`, `restore.json` | Kiểm API/idempotency/cache/quyền/file |
| `evidence/restored-revocation.json`, `restore-session-revocation.json` | Thu hồi quyền public và vô hiệu hóa 4 session cũ |
| `evidence/source-comparison.json`, `build.log` | Hash runtime và build tại SHA hiện tại |
| `evidence/handoff.json` | Trạng thái cuối, hash dump/object, ngân sách và cleanup |

Helper private ghi đúng lệnh đã chạy: `prepare-synthetic.py`, `verify-recovery.py`, `snapshot-db.py`, `backup-restore.ps1`, `start-api.ps1` (chỉ Fake). Chúng áp dụng cho instance test này, không dùng thay runbook DB ứng dụng. Thứ tự đã chạy: seed → verify seed → restart container/API → verify restart → backup/restore → verify restore → kiểm hide → export/replay ledger → kiểm cookie cũ. Không cần lặp lại paid pilot để kiểm chứng các bước này.

API thử đã dừng sau nghiệm thu, container test được dừng nhưng giữ volume bền vững, dump/object trên đĩa vẫn còn. Không xóa container/volume hoặc DB ứng dụng của người dùng. Cookie tạm được xóa; không đưa key/DB password/DP key/capture vào Git hoặc báo cáo.

## Trạng thái giai đoạn C sau bổ sung

- Chức năng local P10–P15 và so sánh synthetic: đã chốt ở report C ngày 22/10; lượt này bổ sung bằng chứng phục hồi, không nhận là đã chạy lại toàn bộ 115 backend/9 E2E/3 frontend.
- **Phần thiếu dump/restore đã được xử lý bằng instance Fake mới**, không biến thành tuyên bố có dump của instance DeepSeek cũ. Không còn yêu cầu kỹ thuật C bị Docker chặn.
- Ledger DeepSeek giữ **0,02/2 USD đã ghi nợ, còn 1,98 USD**, unknown USD = 0; không gọi balance/provider hoặc phát sinh paid call mới. Chi tiêu ngoài app không được quota local tự kiểm soát.
- Chưa có bằng chứng chất lượng đại diện, capacity kho lớn, backup theo lịch/offsite/production hoặc CI mới. Những giới hạn này vẫn được công khai; không tự mở rộng triển khai để thay cho đánh giá có nhãn/người rà.
