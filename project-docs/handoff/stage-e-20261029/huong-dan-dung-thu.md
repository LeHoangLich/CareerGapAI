# Cài và dùng thử bản giao diện mới

## Chuẩn bị trên Windows

Cần Git hoặc gói source đã giải nén, Python 3, Node.js 24.x, .NET SDK theo global.json, Docker Desktop có Linux engine hoạt động. Bản kiểm chứng cụ thể và kết quả chạy được ghi trong biên bản bàn giao, không suy mọi bản Windows/browser đều đã được thử.

Mở PowerShell tại thư mục dự án. Cài dependency frontend từ lockfile:

```powershell
Set-Location D:\CareerGapAI
npm --prefix frontend ci
python scripts/handoff-demo.py start
```

Nếu giải nén ở nơi khác, thay lệnh Set-Location bằng đường dẫn đó. Launcher tự build frontend/backend, migration tường minh và tạo ba tài khoản synthetic trong database/container riêng; không đọc .env hoặc nạp key thật. Cổng DB55449 và web5190 thuộc bản demo này. Nếu cổng đã bận, dừng và xem log thay vì xóa dữ liệu ứng dụng.

Mở http://127.0.0.1:5190/. Tài khoản candidate/recruiter/admin nằm trong `.local/handoff-demo-20261029/accounts.private.md`. Dùng các browser context/cửa sổ riêng nếu cần giữ nhiều vai trò. Nội dung đăng ký/đăng nhập vẫn là dữ liệu private; không đưa vào báo cáo public.

## Điều hướng

| Vai trò | Mục cần mở |
| --- | --- |
| Candidate | Quản lý CV riêng `#private-resumes`; Soạn & Cải thiện CV `#cv-builder`; Hồ sơ `#profile` |
| Candidate | Khám phá việc làm `#jobs`; Đơn ứng tuyển & Thông báo `#applications` |
| Recruiter | Công ty `#recruiter-company`; Tin & JD `#recruiter-jobs`; Ứng viên `#recruiter-applicants` |
| Recruiter | Kho CV công khai; Hồ sơ được chia sẻ theo quyền |
| Admin | Quản trị `#administration` |

Trên mobile, mở nút Mở điều hướng, chọn mục rồi drawer đóng. Mỗi thời điểm chỉ một view gắn vào trang. Đổi view giữ các bản nháp trong phiên; F5 không phải bảo đảm khôi phục mọi nội dung chưa lưu. Phiên đăng xuất hủy state của tài khoản cũ.

## Kịch bản 1 — CV và nguồn dữ liệu

Candidate mở Quản lý CV riêng, tải `CV-synthetic-dung-thu.docx` một lần. Chọn Đọc đầy đủ đúng phiên bản, chờ xử lý và đọc nhãn mô phỏng. Mở phần rà soát, so trường dữ liệu với quote/locator, chỉ xác nhận dữ kiện thực có. Trường thiếu để trống; nội dung tự bổ sung được phân biệt với nguồn file. Upload/xác nhận không tự công khai CV. Ghi kết quả thực tế trong phiếu, không đánh dấu đạt trước thao tác.

## Kịch bản 2 — Tin, đối chiếu và ứng tuyển

Recruiter mở Công ty và tạo hồ sơ synthetic. Mở Tin & JD, tạo tin về React/TypeScript, tách yêu cầu local, rà phân loại rồi xác nhận và công khai. Candidate mở Khám phá việc làm, chọn CV đã xác nhận, xem retrieval và nhóm đối chiếu AI ở cột phải. Hai loại điểm có ý nghĩa khác nhau; lỗi phân tích không phải điểm0.

Candidate chọn đúng CV version rồi nộp một lần. CV riêng có thể dùng nộp, không bắt buộc công khai trước. Recruiter mở Ứng viên, mở danh sách hồ sơ đúng tin, tải CV qua đơn và cập nhật sàng lọc/ghi chú. Đơn giữ snapshot CV/JD lúc nộp; AI không tự đổi trạng thái. Rút đơn có dialog xác nhận, hủy dialog giữ dữ liệu.

## Kịch bản 3 — Viết CV và PDF

Candidate mở Soạn & Cải thiện CV. Tạo từ thông tin synthetic hoặc nạp revision đã xác nhận. Tạo đề xuất, xem badge AI và so với dữ kiện nguồn; chấp nhận chỉ khi đồng ý. Sau khi đưa vào draft, chọn ngôn ngữ heading Việt hoặc English, tạo PDF, kiểm dấu/copy text/bố cục. Nút lưu tạo version mới, giữ version cũ và public selection. Ngôn ngữ heading là lựa chọn trình bày ở frontend, không tự dịch nội dung nghề nghiệp hoặc được ghim thành field revision backend.

## Kịch bản 4 — AI tắt

Lưu xong draft rồi chạy:

```powershell
python scripts/handoff-demo.py fault
```

Tải lại web, thử đọc đầy đủ một CV. Thông báo phải nêu rõ AI đang tắt/chưa tạo yêu cầu, vẫn xem/tải CV và thao tác thủ công được. Không gọi provider thật. Khôi phục bằng `python scripts/handoff-demo.py start`.

## Kết thúc và ghi nhận

Điền người thực hiện, ngày giờ thật, phiên bản, tác vụ, thông báo/lỗi, kết quả PDF và mức trợ giúp. Người đánh giá nhận xét riêng về chất lượng và giới hạn trong báo cáo thực nghiệm. Phiếu mẫu trống không phải biên bản đã nghiệm thu. Phản hồi của owner được tách với giảng viên. Dừng API demo bằng `python scripts/handoff-demo.py stop`; container/volume synthetic được giữ để mở tiếp.
