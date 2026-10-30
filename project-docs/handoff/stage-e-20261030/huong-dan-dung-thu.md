# Chạy và dùng thử bản 30/10

Yêu cầu Windows/Python 3, Node 24.x, .NET SDK theo `global.json`, Docker Desktop Linux engine. Dùng checkout hoặc giải nén gói source mới; thay đường dẫn dưới đây nếu giải nén ở nơi khác.

```powershell
Set-Location D:\CareerGapAI
npm --prefix frontend ci
python scripts/handoff-demo.py start --release 20261030
```

Mở http://127.0.0.1:5190/. Chỉ sử dụng dữ liệu synthetic. Launcher không đọc `.env`; AI/search/email đều Fake và key trống. Tài khoản Candidate/Recruiter/Admin private nằm tại `.local/handoff-demo-20261029/accounts.private.md`. Trên máy đang có demo, giữ nguyên tài khoản, CV, đơn và ghi chú cũ; trên máy mới launcher tạo môi trường synthetic riêng, không có sẵn dữ liệu diễn tập của máy khác.

## Xem lại kết quả CV–JD

1. Candidate mở **Khám phá việc làm**, chọn đúng CV và phiên bản đã phân tích, bấm **Tìm việc theo CV**. CV không cần công khai để chính chủ tìm việc.
2. Ở dòng tin trong kết quả từ khóa, bấm **Xem kết quả đã lưu**. Nếu chưa có kết quả được phép xem, UI báo rõ; nút này không tự chạy AI.
3. Nếu đã có kết quả, UI hiển thị lần phân tích gần nhất của chính tài khoản trong đúng ngữ cảnh. Đọc thông báo dữ liệu/phương pháp thuộc lần trước; đây không phải đánh giá lại CV/JD hiện tại.
4. Mở **Xem tiêu chí và bằng chứng**, đối chiếu nguồn và nhãn Fake. Với tiêu chí một phần/chưa có bằng chứng, có thể mở **Tìm nguồn học cho tiêu chí này**. Search Fake trả trạng thái mô phỏng, chưa tìm web thật.
5. Tải lại toàn trang, tìm lại cùng cặp và bấm **Xem kết quả đã lưu**; không cần biết job ID hoặc sửa idempotency key.

Nếu muốn phân tích mới, dùng nút **Phân tích cặp CV–JD** riêng; đó là thao tác tạo yêu cầu, khác việc xem lại. Kết quả không có quyền hiện tại sẽ không được trả, kể cả đã từng xem được. Ngữ cảnh hồ sơ ứng tuyển vẫn dùng CV/JD lúc nộp; không lấy JD mới để thay snapshot.

Trên máy hiện tại có thể dùng CV `SIM-AI-U1-20261030`, v1 và tin `DEMO-PHU — React và Docker`. Nếu không có dữ liệu đó, dùng [CV synthetic](CV-synthetic-dung-thu.docx) và [JD synthetic](JD-synthetic-dung-thu.txt), thực hiện luồng [chuẩn bị CV/JD của bản nền](../stage-e-20261029/huong-dan-dung-thu.md) trước. Không chạy lại trích xuất/ứng tuyển chỉ để nhân bản dữ liệu cũ.

## AI tắt và kết thúc

```powershell
python scripts/handoff-demo.py fault --release 20261030
```

Yêu cầu AI mới phải bị từ chối rõ ràng; kết quả đã lưu vẫn xem theo quyền. Xem/tải CV, đơn và soạn PDF thủ công không phụ thuộc AI. Khôi phục bằng lệnh `start --release 20261030`.

```powershell
python scripts/handoff-demo.py stop
```

Lệnh stop chỉ dừng API thuộc demo, giữ container/database/file. Không dùng lệnh xóa volume. Phiếu mẫu là [phiếu trống](Phieu-dung-thu-CareerGapAI.docx), không phải bằng chứng đã có người đánh giá thật.
