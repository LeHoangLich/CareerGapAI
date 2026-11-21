# Dùng thử release20261031

Yêu cầu Windows, Python3, Node theo.node-version, .NET theo global.json và Docker Desktop Linux engine. Mở checkout/gói nguồn; chuẩn bị dependency frontend nếu máy mới.

```powershell
Set-Location D:\CareerGapAI
python scripts/handoff-demo.py start --release 20261031
```

Mở http://127.0.0.1:5190/. `start` không ghi release cũng mặc định31. Launcher chỉ dùng Fake, key trống, không đọc.env. Dùng tài khoản synthetic private tại `.local/handoff-demo-20261029/accounts.private.md`, không đưa vào báo cáo.

## Xem lại kết quả cặp

1. Đăng nhập Candidate → Khám phá việc làm → chọn CV/version đã phân tích → Tìm việc theo CV.
2. Bấm **Xem kết quả đã lưu**, đọc tiêu chí/nguồn. Reload và mở lại cùng cặp; GET không tạo job mới.
3. Mở tiêu chí Docker → Tìm nguồn học cho tiêu chí này. Search ở đây Fake; không dùng làm bằng chứng video thật/tiếng Việt.

Máy hiện tại có `SIM-AI-U1-20261030` v1 và `DEMO-PHU — React và Docker`. Máy mới dùng CV/JD synthetic trong bộ này và hướng dẫn chuẩn bị của [bản nền](../stage-e-20261029/huong-dan-dung-thu.md); không mặc định máy mới có dữ liệu lịch sử.

## Reset mật khẩu A12

1. Đăng xuất hoặc mở cửa sổ riêng. Có thể kiểm lỗi bằng URL local `/?resetEmail=synthetic%40example.invalid&resetToken=synthetic-invalid#access`.
2. Nhập mật khẩu thử đúng độ dài12–256, bấm Đặt lại mật khẩu. API400 invalid_or_expired_reset hiện thông báo link không hợp lệ/hết hạn.
3. Bấm **Yêu cầu gửi lại liên kết mới**: form khôi phục mở, lỗi/querytoken cũ biến mất; không tự gửi email.
4. Nếu thử luồng đầy đủ, dùng tài khoản synthetic của demo → gửi hướng dẫn → hộp thư Fake local → reset. Không đổi mật khẩu tài khoản demo dùng chung trừ khi lưu cập nhật private để lần diễn tập sau đăng nhập được. Kiểm tự động trên5190 chỉ dùng invalid token, không đổi mật khẩu hoặc gửi email.

Backend regression đã kiểm token rỗng/sai/hết hạn/đã dùng; E2E kiểm used token→forgot→gửi mới. HTTP500/503/mạng hiện lỗi hệ thống; password_policy có thông báo12–256. Không suy used/expired cụ thể chỉ từ cùng mã lỗi.

## AI tắt

```powershell
python scripts/handoff-demo.py fault --release 20261031
```

Yêu cầu AI mới trả503/ai_disabled; kết quả cũ, tảiCV và nội dung builder thủ công vẫn đọc được. Khôi phục bằng start31. [Vận hành/rollback](van-hanh-local.md) giải thích giới hạn và bảo toàn dữ liệu.
