# Cải thiện lỗi đặt lại mật khẩu — 31/10/2026

## Tái kiểm theo chỉ thị owner — 31/10

Lượt mới `20261031-172834-d216` chạy đầy đủ `python scripts/test-frontend-regression.py`: backend **160/160**, frontend **15/15**, Playwright **22/22**, lint/typecheck/build/migration/seed đều đạt. Runner hoàn tất và cleanup=true; Fake-only, không nạp dotenv, non_fake_methods=0, usd_budgets=0. Backend build 0 warnings/errors; Vite có cảnh báo bundle >500 kB không chặn build, không che giấu hoặc đổi ngưỡng để bỏ cảnh báo.

Manifest cùng tên đã ghim SHA-256 `result.json`, `results/backend.trx`, source manifest và toàn bộ logs trong `.local/frontend-step3-20261031-172834-d216/`; giữ kết quả lượt trước ở `previous_verification`. Kiểm hash 126 file nguồn/ledger không đổi trong lượt tái kiểm. Không sửa thêm code.

Phân biệt bằng chứng: backend kiểm token hết hạn bằng thời gian chờ thực; E2E kiểm link **đã dùng**, nhận cùng mã invalid_or_expired_reset, hiện nút yêu cầu link mới → forgot → gửi yêu cầu mới. Không gọi đây là E2E chờ token hết hạn. E2E riêng mock policy/500/503/network cũng đạt.

Auditor: Codex (Authorized by Project Owner); approved_by: Lê Hoàng Lịch (Project Owner), căn cứ chỉ thị thực thi; status: verified; human_authorized: true. Demo5190/bộ bàn giao cũ chưa cập nhật.

Hoàn thành sửa mã nguồn và kiểm thử local theo ủy quyền của Lê Hoàng Lịch. Người thực hiện: Codex. Đây là cập nhật xử lý mục A12 của báo cáo feedback-triage-2026-10-30; không sửa kết quả nghiệm thu lịch sử.

## Hành vi sau sửa

- HTTP 400 `invalid_or_expired_reset`: “Liên kết đặt lại mật khẩu không hợp lệ hoặc đã hết hạn. Vui lòng yêu cầu liên kết mới.” Áp dụng token rỗng, sai, đã dùng hoặc hết hạn.
- HTTP 400 `password_policy`: “Mật khẩu mới phải có độ dài từ 12 đến 256 ký tự.” Backend kiểm độ dài trước tra tài khoản/token.
- HTTP 500/503 hoặc lỗi kết nối: “Máy chủ gặp sự cố hoặc kết nối bị gián đoạn. Vui lòng thử lại sau.” Thông báo riêng `ai_disabled` được giữ cho luồng AI tắt đã nghiệm chứng.
- Frontend giữ mã/status bằng `ApiError`; chỉ hiện nút “Yêu cầu gửi lại liên kết mới” khi nhận HTTP 400 với mã invalid reset. Nút mở form khôi phục, xóa lỗi cũ và query chứa token khỏi URL, không tự gửi email. Nút quay lại đăng nhập vẫn có.
- Giữ HTTP 400, cơ chế token dùng một lần, bảo vệ thông tin tài khoản và xử lý hủy request chủ động.

## Kiểm chứng

Chạy `python scripts/test-frontend-regression.py`, run `20261030-221503-87b8` (mã run giữ theo thời điểm máy thực thi; ngày lập biên bản theo phiên làm việc 31/10).

| Kiểm tra | Kết quả |
|---|---|
| Backend trên PostgreSQL tạm riêng | 160/160 đạt, 0 bỏ qua |
| Frontend unit | 15/15 đạt |
| Lint / typecheck / build | Đạt cả ba |
| Playwright E2E toàn bộ | 22/22 đạt |
| Migration / seed / runner checks | Đạt |
| Dọn môi trường test | Hoàn thành |

Backend kiểm null/rỗng/11/257 ký tự; token rỗng/sai/hết hạn/đã dùng; tài khoản không tồn tại; reset thành công và đăng nhập bằng mật khẩu mới. E2E chạy luồng reset với backend thật và hộp thư Fake, dùng lại link bị từ chối, chuyển forgot rồi gửi yêu cầu mới. Một ca E2E mock riêng kiểm nhánh policy/500/503/mất mạng và không hiện nút sai ngữ cảnh. Unit kiểm thêm lỗi CSRF/kết quả không phải JSON và giữ AbortError.

Bằng chứng local: `.local/frontend-step3-20261030-221503-87b8/result.json`, `results/backend.trx`, `backend-tests.log`, `frontend-test.log`, `e2e.log`. Result xác nhận `providers=Fake`, `dotenvLoaded=false`, `non_fake_methods=0`, `usd_budgets=0`, `completed=true`, `cleanup=true`. Không gọi provider thật hoặc phát sinh phí.

Lượt khởi động trước đó (`20261030-221410-4153`) dừng vì Docker engine chưa chạy; đã mở Docker và chạy lại đầy đủ đạt. Không coi lượt dừng là đạt.

## Phạm vi bàn giao

A12 hoàn thành ở mã nguồn và bộ kiểm thử hiện tại. Runtime demo 5190 và gói bàn giao 30/10 chưa được đóng gói lại với thay đổi này. Không thay corpus, nhãn, metric, ledger, tài liệu Word của owner hoặc nghiệm thu M3/M5/M6. M6 vẫn chờ đánh giá ngoài đang hoãn.
